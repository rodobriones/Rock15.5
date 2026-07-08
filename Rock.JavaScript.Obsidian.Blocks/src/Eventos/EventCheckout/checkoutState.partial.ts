// Estado compartido del checkout de eventos. Un solo composable crea TODOS los refs, computeds
// y acciones (los 5 pasos comparten estado: selección, asistentes, hold, promo, NIT, pasarela);
// el shell (eventCheckout.obs) lo crea y lo provee, y cada *.partial.obs de paso lo inyecta y
// destructura SOLO lo que su template usa (el destructuring está tipado: un miembro inexistente
// no compila).
import { computed, inject, onUnmounted, provide, ref, watch, type InjectionKey } from "vue";
import RockForm from "@Obsidian/Controls/rockForm.obs";
import { provideSubmitPayment } from "@Obsidian/Core/Controls/financialGateway";
import { PromiseCompletionSource } from "@Obsidian/Utility/promiseUtils";
import { newGuid } from "@Obsidian/Utility/guid";
import { FormError } from "@Obsidian/Utility/form";
import { GatewayControlBag } from "@Obsidian/ViewModels/Controls/gatewayControlBag";
import { useBlockActionUrl, useConfigurationValues, useInvokeBlockAction } from "@Obsidian/Utility/block";
import {
    ApplyPromoResponseBag,
    CreateHoldResponseBag,
    EventBag,
    InitBag,
    ProcessCheckoutResponseBag,
    TicketTypeBag
} from "./types.partial";
import { createAttendeeState } from "./attendeeState.partial";

// Fonts (Roboto + Roboto Mono) — <link> para no bloquear el render. Una vez por página.
if (typeof document !== "undefined" && !document.getElementById("ec-fonts")) {
    const link = document.createElement("link");
    link.id = "ec-fonts";
    link.rel = "stylesheet";
    link.href = "https://fonts.googleapis.com/css2?family=Roboto:wght@400;500;700&family=Roboto+Mono:wght@500;700&display=swap";
    document.head.appendChild(link);
}

// eslint-disable-next-line @typescript-eslint/explicit-function-return-type
function createCheckoutState() {
    const config = useConfigurationValues<InitBag>();
    const invokeBlockAction = useInvokeBlockAction();

    const stepLabels = ["Entradas", "Asistentes", "Revisión", "Pago", "Listo"];

    // #region State

    const step = ref(1);
    const busy = ref(false);
    const err = ref("");

    const ticketTypes = ref<TicketTypeBag[]>(config.ticketTypes ?? []);
    const quantities = ref<Record<number, number>>({});

    // El evento vive en un ref (no directo de config): un evento con contraseña llega LIMITADO
    // en el init (sin descripción/tipos) y UnlockEvent lo completa aquí.
    const event = ref<EventBag | null>(config.event ?? null);

    // Gate de contraseña (visibilidad "Con contraseña"). La contraseña se conserva en memoria y
    // viaja en cada acción de venta: el servidor re-valida siempre (no confía en el desbloqueo).
    const passwordRequired = ref<boolean>(config.requiresPassword === true);
    const accessPassword = ref<string>("");
    const unlockBusy = ref<boolean>(false);
    const unlockError = ref<string>("");

    async function unlockEvent(): Promise<void> {
        if (!accessPassword.value.trim() || unlockBusy.value) {
            return;
        }
        unlockBusy.value = true;
        unlockError.value = "";
        try {
            const res = await invokeBlockAction<{ event: EventBag; ticketTypes: TicketTypeBag[] }>("UnlockEvent", {
                bag: { password: accessPassword.value.trim() }
            });
            if (res.isSuccess && res.data?.event) {
                event.value = res.data.event;
                ticketTypes.value = res.data.ticketTypes ?? [];
                passwordRequired.value = false;
            }
            else {
                unlockError.value = res.errorMessage || "No se pudo validar la contraseña.";
            }
        }
        catch {
            unlockError.value = "Error validando la contraseña.";
        }
        finally {
            unlockBusy.value = false;
        }
    }

    // Asistentes y preguntas (unidades, prefill, validación, acordeón, buildLines):
    // sub-composable propio — ver attendeeState.partial.ts.
    const attendee = createAttendeeState({ config, invokeBlockAction, ticketTypes, getQty });
    const { buildAttendeeUnits, loadFamilyMembers, buildLines } = attendee;

    // Correo al que se envían las entradas (paso 4): precargado con el del perfil, editable.
    // Reemplazarlo NO actualiza el perfil (solo el envío); si el perfil no tenía correo, el
    // servidor se lo guarda al confirmar el pago.
    const deliveryEmail = ref((config.currentPersonEmail || "").trim());
    const deliveryEmailValid = computed<boolean>(() => /^\S+@\S+\.\S+$/.test(deliveryEmail.value.trim()));

    const wantsInvoice = ref(false);
    const nit = ref("");
    const invoiceName = ref("");   // razón social validada en SAT (readonly; se manda como InvoiceName)
    const nitAddress = ref("");
    const busyNit = ref(false);
    const nitError = ref("");

    // Sanea el NIT (solo alfanumérico, mayúsculas) e invalida la razón social previa al editarlo.
    watch(nit, (val) => {
        const clean = (val || "").replace(/[^0-9a-zA-Z]/g, "").toUpperCase();
        if (clean !== val) {
            nit.value = clean;   // re-dispara el watch una vez; clean ya saneado => no loop
            return;
        }
        invoiceName.value = "";
        nitAddress.value = "";
        nitError.value = "";
    });

    // Al apagar el toggle de factura, limpiar todo (se factura como CF).
    watch(wantsInvoice, (on) => {
        if (!on) {
            nit.value = "";
            invoiceName.value = "";
            nitAddress.value = "";
            nitError.value = "";
        }
    });

    // Promo code (validado server-side; el descuento del cliente es solo visual).
    const promoInput = ref("");
    const appliedPromo = ref<{ code: string; discountTotal: number; description: string } | null>(null);
    const promoBusy = ref(false);
    const promoError = ref("");

    const gatewayControlModel = ref<GatewayControlBag | null>(null);
    const gatewayError = ref("");
    const gatewayValidationFields = ref<FormError[]>([]);
    const paymentForm = ref<InstanceType<typeof RockForm>>();
    let gatewayCompletionSource: PromiseCompletionSource | undefined;

    const confirmation = ref<ProcessCheckoutResponseBag | null>(null);

    // PaymentReference is generated once per checkout attempt for backend idempotency.
    // Regenerated each time a new hold is created.
    let paymentReference = newGuid();

    // Hold (reserva temporal que aparta el cupo mientras se paga) + contador de pago.
    const holdActive = ref(false);
    const holdExpired = ref(false);
    const holdRemaining = ref(0); // segundos restantes
    const holdTotalSeconds = ref(600); // ventana total del hold (para la barra de progreso)
    let holdTimerId: number | undefined;
    // Huella de la selección con la que se creó el hold vigente: navegar atrás/adelante NO toca la
    // reserva; solo se re-reserva si las cantidades cambiaron (o no hay hold vigente). El hold se
    // consume únicamente al pagar, al expirar o al abandonar la página.
    let heldQuantitiesKey = "";

    function quantitiesKey(): string {
        return Object.entries(quantities.value)
            .filter(([, q]) => q > 0)
            .sort(([a], [b]) => Number(a) - Number(b))
            .map(([id, q]) => `${id}:${q}`)
            .join("|");
    }

    const submitGatewayPayment = provideSubmitPayment();

    // #endregion

    // #region Computed

    // Estilo del header elegido por el organizador (default persistente = hero grande).
    const slimHeader = computed(() => event.value?.headerStyle === "condensado");

    // El evento ya terminó: la venta está cerrada (el servidor también lo valida en CreateHold/ProcessCheckout).
    const eventEnded = computed(() => {
        const end = event.value?.endDateTime;
        if (!end) {
            return false;
        }
        const d = new Date(end);
        return !isNaN(d.getTime()) && d.getTime() < Date.now();
    });

    // Slug de la categoría para el color del badge (ecBadge--conferencia, etc.).
    const categorySlug = computed(() => (event.value?.category ?? "").toLowerCase());

    const eventSubtitle = computed(() => {
        const ev = event.value;
        if (!ev) {
            return "";
        }
        const parts: string[] = [];
        if (ev.venueName) {
            parts.push(ev.venueName);
        }
        if (ev.campusName) {
            parts.push(ev.campusName);
        }
        return parts.join(" · ");
    });

    const eventDateLabel = computed(() => {
        const ev = event.value;
        if (!ev?.startDateTime) {
            return "";
        }
        const d = new Date(ev.startDateTime);
        if (isNaN(d.getTime())) {
            return "";
        }
        return d.toLocaleString("es-GT", { weekday: "long", day: "2-digit", month: "long", hour: "2-digit", minute: "2-digit" });
    });

    // Fecha + rango horario para el detalle del evento, ej: "sábado, 04 de julio de 2026 · 08:00 → 13:00".
    const eventDateRange = computed(() => {
        const ev = event.value;
        if (!ev?.startDateTime) {
            return "";
        }
        const s = new Date(ev.startDateTime);
        if (isNaN(s.getTime())) {
            return "";
        }
        const dateStr = s.toLocaleDateString("es-GT", { weekday: "long", day: "2-digit", month: "long", year: "numeric" });
        const timeOpts = { hour: "2-digit", minute: "2-digit" } as const;
        let timeStr = s.toLocaleTimeString("es-GT", timeOpts);
        if (ev.endDateTime) {
            const e = new Date(ev.endDateTime);
            if (!isNaN(e.getTime())) {
                timeStr = `${timeStr} → ${e.toLocaleTimeString("es-GT", timeOpts)}`;
            }
        }
        return `${dateStr} · ${timeStr}`;
    });

    const hasSelection = computed(() => Object.values(quantities.value).some(q => q > 0));

    // Barra de progreso: 0% en el paso 1, 100% en el paso 5 (Listo).
    const progressPct = computed(() => ((step.value - 1) / (stepLabels.length - 1)) * 100);

    const summaryRows = computed(() => {
        return ticketTypes.value
            .filter(tt => getQty(tt.id) > 0)
            .map(tt => {
                const quantity = getQty(tt.id);
                return {
                    ticketTypeId: tt.id,
                    name: tt.name,
                    quantity,
                    lineTotal: quantity * tt.effectivePrice
                };
            });
    });

    const total = computed(() => summaryRows.value.reduce((sum, r) => sum + r.lineTotal, 0));

    // Descuento aplicado (0 si no hay promo) y total final a cobrar.
    const discount = computed(() => appliedPromo.value?.discountTotal ?? 0);
    const grandTotal = computed(() => Math.max(0, total.value - discount.value));

    // Entrada gratuita: total 0 (precio 0 o promo que lo deja en 0) ⟹ no se muestra la pasarela.
    // El servidor decide de forma autoritativa por el Total; esto solo cambia la UI del paso de pago.
    const isFree = computed(() => grandTotal.value <= 0);

    // IVA Guatemala 12% incluido en el precio (no se suma): valor informativo desglosado del total final.
    const ivaIncluded = computed(() => grandTotal.value - grandTotal.value / 1.12);

    // Contador de pago "MM:SS" a partir de los segundos restantes del hold.
    const holdCountdownLabel = computed(() => {
        const s = Math.max(0, holdRemaining.value);
        const m = Math.floor(s / 60);
        const sec = s % 60;
        return `${m}:${sec.toString().padStart(2, "0")}`;
    });

    // Porcentaje restante del hold (para la barra que se vacía).
    const holdProgressPct = computed(() => {
        const total = holdTotalSeconds.value || 1;
        return Math.max(0, Math.min(100, (holdRemaining.value / total) * 100));
    });

    // #endregion

    // Reloj ligero para el countdown de early-bird (granularidad de minuto; se limpia al desmontar).
    const now = ref(Date.now());
    const clockId = window.setInterval(() => { now.value = Date.now(); }, 60000);
    // Liberar el hold si el usuario cierra o recarga la pestaña (onUnmounted no corre en unload).
    // sendBeacon sobrevive al cierre de la página; el body replica el contrato de invokeBlockAction
    // (POST JSON a /api/v2/BlockActions/... con { __context, bag }, auth por cookie). Si falla o el
    // navegador no lo soporta, el hold expira solo por tiempo (10 min).
    const blockActionUrl = useBlockActionUrl();
    function releaseHoldOnPageHide(): void {
        if (!holdActive.value) {
            return;
        }
        holdActive.value = false;
        stopHoldTimer();
        try {
            const body = new Blob(
                [JSON.stringify({ __context: { pageParameters: {} }, bag: { paymentReference } })],
                { type: "application/json" });
            navigator.sendBeacon(blockActionUrl("ReleaseHold"), body);
        }
        catch {
            // Best-effort.
        }
    }
    window.addEventListener("pagehide", releaseHoldOnPageHide);

    onUnmounted(() => {
        window.clearInterval(clockId);
        window.removeEventListener("pagehide", releaseHoldOnPageHide);
        stopHoldTimer();
        // Best-effort: liberar el hold al salir (si no completa, expira solo por tiempo).
        void releaseHold();
    });

    // #region Functions

    function formatCurrency(value: number): string {
        return new Intl.NumberFormat("es-GT", { style: "currency", currency: "GTQ" }).format(value || 0);
    }

    // "Descargar PDF": pide al servidor el PDF real de los boletos (un boleto por página, un QR por
    // ticket, generado con el PdfGenerator de Rock) y lo descarga como archivo. La primera generación
    // en el servidor puede tardar. Fallback: imprimir la confirmación si el backend falla.
    const pdfBusy = ref(false);
    async function downloadPdf(): Promise<void> {
        if (pdfBusy.value) {
            return;
        }
        pdfBusy.value = true;
        try {
            const res = await invokeBlockAction<{ fileName: string; pdfBase64: string }>("GetTicketsPdf", {
                bag: { paymentReference }
            });
            if (res.isSuccess && res.data?.pdfBase64) {
                const bytes = Uint8Array.from(atob(res.data.pdfBase64), c => c.charCodeAt(0));
                const url = URL.createObjectURL(new Blob([bytes], { type: "application/pdf" }));
                const a = document.createElement("a");
                a.href = url;
                a.download = res.data.fileName || "entradas.pdf";
                document.body.appendChild(a);
                a.click();
                a.remove();
                URL.revokeObjectURL(url);
            }
            else {
                window.print();
            }
        }
        catch {
            window.print();
        }
        finally {
            pdfBusy.value = false;
        }
    }

    // "Agregar a Wallet" (Listo): Apple en iPhone/iPad/Mac, Google en el resto — el botón solo
    // aparece si la plataforma del dispositivo está configurada (un .pkpass no sirve en Android
    // y viceversa). El server empaqueta TODAS las entradas de la orden en una sola hoja.
    const isApplePlatform = /iPhone|iPad|iPod|Macintosh/i.test(navigator.userAgent);
    const walletKind = computed<"apple" | "google" | null>(() => {
        if (isApplePlatform && config.appleWalletEnabled) {
            return "apple";
        }
        if (!isApplePlatform && config.googleWalletEnabled) {
            return "google";
        }
        return null;
    });
    const walletBusy = ref(false);
    const walletError = ref("");
    async function addToWallet(): Promise<void> {
        if (walletBusy.value || !walletKind.value) {
            return;
        }
        walletBusy.value = true;
        walletError.value = "";
        try {
            const action = walletKind.value === "apple" ? "GetApplePasses" : "GetGoogleWalletUrl";
            const res = await invokeBlockAction<{ url: string }>(action, { bag: { paymentReference } });
            if (!res.isSuccess || !res.data?.url) {
                walletError.value = res.errorMessage || "No se pudo generar el pase.";
                return;
            }
            // Navegación real (no fetch): Safari abre la hoja de Wallet por el MIME pkpass(es);
            // el link de Google abre su página de guardado.
            window.location.href = res.data.url;
        }
        catch {
            walletError.value = "Error de red. Intenta de nuevo.";
        }
        finally {
            walletBusy.value = false;
        }
    }

    // "Volver al inicio": vuelve a la portada del sitio.
    function goHome(): void {
        // "Volver al inicio" lleva al calendario público de eventos (LinkedPage "Calendar Page").
        window.location.href = config.calendarUrl || "/";
    }

    function formatShortDate(value: string): string {
        const d = new Date(value);
        return isNaN(d.getTime()) ? "" : d.toLocaleDateString("es-GT", { day: "2-digit", month: "2-digit" });
    }

    // Cuenta regresiva early-bird: "3d 5h" o "5h 20m". Vacío si ya venció o sin fecha. Reactivo vía `now`.
    function earlyBirdCountdown(tt: TicketTypeBag): string {
        if (!tt.earlyBirdUntil) {
            return "";
        }
        const end = new Date(tt.earlyBirdUntil).getTime();
        const diff = end - now.value;
        if (isNaN(end) || diff <= 0) {
            return "";
        }
        const mins = Math.floor(diff / 60000);
        const d = Math.floor(mins / 1440);
        const h = Math.floor((mins % 1440) / 60);
        const m = mins % 60;
        return d > 0 ? `${d}d ${h}h` : `${h}h ${m}m`;
    }

    function getQty(ticketTypeId: number): number {
        return quantities.value[ticketTypeId] ?? 0;
    }

    function maxQtyFor(tt: TicketTypeBag): number {
        let max = tt.maxPerOrder ?? 99;
        if (tt.remaining != null) {
            max = Math.min(max, tt.remaining);
        }
        return Math.max(0, max);
    }

    function setQty(tt: TicketTypeBag, value: number): void {
        quantities.value = { ...quantities.value, [tt.id]: Math.max(0, Math.min(value, maxQtyFor(tt))) };
        // El descuento depende de la selección: al cambiar cantidades se invalida y hay que re-aplicar.
        if (appliedPromo.value) {
            appliedPromo.value = null;
            promoError.value = "";
        }
    }

    async function applyPromo(): Promise<void> {
        const code = promoInput.value.trim();
        if (!code) {
            promoError.value = "Ingresa un código.";
            return;
        }
        promoBusy.value = true;
        promoError.value = "";
        try {
            const res = await invokeBlockAction<ApplyPromoResponseBag>("ApplyPromoCode", {
                bag: { code, lines: buildLines(), accessPassword: accessPassword.value }
            });
            if (res.isSuccess && res.data) {
                appliedPromo.value = {
                    code: res.data.code,
                    discountTotal: res.data.discountTotal,
                    description: res.data.description
                };
                promoError.value = "";
            }
            else {
                appliedPromo.value = null;
                promoError.value = res.errorMessage || "No se pudo aplicar el código.";
            }
        }
        catch {
            appliedPromo.value = null;
            promoError.value = "Error aplicando el código.";
        }
        finally {
            promoBusy.value = false;
        }
    }

    function removePromo(): void {
        appliedPromo.value = null;
        promoInput.value = "";
        promoError.value = "";
    }

    async function reloadTicketTypes(): Promise<void> {
        busy.value = true;
        err.value = "";
        try {
            const res = await invokeBlockAction<{ ticketTypes: TicketTypeBag[] }>("GetTicketTypes", {
                bag: { password: accessPassword.value }
            });
            if (res.isSuccess && res.data) {
                ticketTypes.value = res.data.ticketTypes ?? [];
            }
            else {
                err.value = res.errorMessage || "No se pudo actualizar la disponibilidad.";
            }
        }
        catch {
            err.value = "Error actualizando disponibilidad.";
        }
        finally {
            busy.value = false;
        }
    }

    async function goToAttendees(): Promise<void> {
        err.value = "";
        // Reservar el cupo AQUÍ (al salir de Entradas): si está agotado, el cliente se entera de
        // inmediato — no después de llenar los datos de los asistentes. La reserva se crea sin
        // asistentes; se amarran al pagar (el servidor los aplica sobre los tickets reservados).
        // Si ya hay un hold vigente con LA MISMA selección (volvió atrás sin cambiar cantidades),
        // se reutiliza: el contador NO se reinicia.
        if (!holdActive.value || holdExpired.value || quantitiesKey() !== heldQuantitiesKey) {
            const ok = await createHold();
            if (!ok) {
                await reloadTicketTypes();
                return;
            }
        }
        await loadFamilyMembers();
        buildAttendeeUnits();
        step.value = 2;
    }

    // Paso 2 -> 3 (Revisión). No reserva cupo todavía (el hold se crea al entrar a Pago).
    function goToReview(): void {
        err.value = "";
        step.value = 3;
    }

    // Valida el NIT contra SAT (ValidateNitInfo) y muestra la razón social.
    async function validateNit(): Promise<void> {
        if (busyNit.value || !nit.value) {
            return;
        }
        const sentNit = nit.value;
        busyNit.value = true;
        nitError.value = "";
        try {
            const response = await invokeBlockAction<{ name: string; address: string }>("ValidateNitInfo", { nit: sentNit });

            // Si el usuario cambió el NIT mientras el request estaba en vuelo, descartar la respuesta.
            if (nit.value !== sentNit) {
                return;
            }

            if (response.isSuccess && response.data?.name) {
                invoiceName.value = response.data.name;
                nitAddress.value = response.data.address || "";
            }
            else {
                nitError.value = response.errorMessage || "NIT inválido o no encontrado.";
                invoiceName.value = "";
                nitAddress.value = "";
            }
        }
        catch {
            if (nit.value === sentNit) {
                nitError.value = "Error de conexión al validar NIT.";
                invoiceName.value = "";
                nitAddress.value = "";
            }
        }
        finally {
            busyNit.value = false;
        }
    }

    // Paso 3 (Revisión) -> 4 (Pago): reserva el cupo (hold) y carga la pasarela. Si ya hay
    // un hold vigente, se reutiliza. Si la reserva falla (p. ej. agotado), se queda en Revisión.
    async function goToPayment(): Promise<void> {
        err.value = "";
        gatewayError.value = "";
        // Si pidió factura (y hay costo), exigir un NIT validado (razón social de SAT) antes de reservar/pagar.
        if (!isFree.value && wantsInvoice.value && !invoiceName.value) {
            err.value = "Valida el NIT antes de continuar, o desactiva la opción de factura.";
            return;
        }
        if (!holdActive.value || holdExpired.value) {
            const ok = await createHold();
            if (!ok) {
                return;
            }
        }
        step.value = 4;
        // Entrada gratuita: no se carga la pasarela (se confirma sin pago).
        if (!isFree.value && !gatewayControlModel.value) {
            await loadGatewayControl();
        }
    }

    // Paso 4 (Pago) -> 3 (Revisión): la reserva SIGUE VIVA (solo se consume al pagar, expirar o
    // abandonar); únicamente se descarga la pasarela para recargarla limpia al volver.
    function backToReview(): void {
        gatewayControlModel.value = null;
        gatewayError.value = "";
        step.value = 3;
    }

    // Crea una reserva temporal del cupo. Regenera el PaymentReference para una orden fresca.
    async function createHold(): Promise<boolean> {
        busy.value = true;
        err.value = "";
        paymentReference = newGuid();
        try {
            const res = await invokeBlockAction<CreateHoldResponseBag>("CreateHold", {
                bag: { lines: buildLines(), paymentReference, accessPassword: accessPassword.value }
            });
            if (res.isSuccess && res.data) {
                paymentReference = res.data.paymentReference || paymentReference;
                holdTotalSeconds.value = res.data.holdSeconds || 600;
                holdActive.value = true;
                holdExpired.value = false;
                heldQuantitiesKey = quantitiesKey();
                startHoldTimer(res.data.expiresDateTime);
                return true;
            }
            err.value = res.errorMessage || "No se pudo reservar tus entradas.";
            return false;
        }
        catch {
            err.value = "Error creando la reserva.";
            return false;
        }
        finally {
            busy.value = false;
        }
    }

    // Libera la reserva en el servidor (devuelve el cupo de inmediato). Best-effort.
    async function releaseHold(): Promise<void> {
        stopHoldTimer();
        if (!holdActive.value) {
            return;
        }
        holdActive.value = false;
        const ref = paymentReference;
        try {
            await invokeBlockAction("ReleaseHold", { bag: { paymentReference: ref } });
        }
        catch {
            // Silencioso: si falla, el hold expira solo por tiempo.
        }
    }

    function startHoldTimer(expiresIso: string): void {
        stopHoldTimer();
        const exp = new Date(expiresIso).getTime();
        holdExpired.value = false;
        const tick = (): void => {
            const remMs = exp - Date.now();
            holdRemaining.value = Math.max(0, Math.ceil(remMs / 1000));
            if (remMs <= 0) {
                void onHoldExpired();
            }
        };
        tick();
        holdTimerId = window.setInterval(tick, 1000);
    }

    function stopHoldTimer(): void {
        if (holdTimerId) {
            window.clearInterval(holdTimerId);
            holdTimerId = undefined;
        }
    }

    // Al expirar el contador: libera, regresa a elegir entradas y refresca disponibilidad.
    // Lo tecleado en Asistentes NO se pierde (buildAttendeeUnits reusa las unidades por key).
    async function onHoldExpired(): Promise<void> {
        // Guard: si hay un pago en vuelo (tokenizando o ProcessCheckout en curso) NO liberar el hold ni
        // devolver atrás — el servidor ya tiene el mutex de cobro sobre esta orden y liberar aquí
        // provocaría un ReleaseHold sobre una orden que se está cobrando y un salto de paso a medias.
        // El timer se detiene igual; si el pago falla, el flujo de error re-reserva.
        stopHoldTimer();
        if (busy.value) {
            return;
        }
        holdExpired.value = true;
        await releaseHold();
        gatewayControlModel.value = null;
        step.value = 1;
        err.value = "Tu reserva expiró. Vuelve a continuar para reservar de nuevo (tus datos se conservan).";
        await reloadTicketTypes();
    }

    // Volver a elegir entradas: la reserva SIGUE VIVA. Si cambia cantidades, el próximo
    // "Continuar" re-reserva (el servidor libera el hold anterior); si no cambia nada, el mismo
    // hold y contador continúan. Abandonar en el paso 1 deja que expire solo (10 min).
    function backToTickets(): void {
        gatewayControlModel.value = null;
        step.value = 1;
    }

    async function loadGatewayControl(): Promise<void> {
        busy.value = true;
        try {
            const res = await invokeBlockAction<{ fileUrl: string; settings: unknown }>("GetGatewayControl", {});
            if (res.isSuccess && res.data?.fileUrl) {
                gatewayControlModel.value = { fileUrl: res.data.fileUrl, settings: res.data.settings };
            }
            else {
                gatewayError.value = res.errorMessage || "No se pudo cargar la pasarela de pago.";
            }
        }
        catch {
            gatewayError.value = "Error cargando la pasarela de pago.";
        }
        finally {
            busy.value = false;
        }
    }

    async function submitCheckout(token: string): Promise<void> {
        busy.value = true;
        err.value = "";
        try {
            const res = await invokeBlockAction<ProcessCheckoutResponseBag>("ProcessCheckout", {
                bag: {
                    lines: buildLines(),
                    gatewayToken: token,
                    paymentReference,
                    nit: nit.value,
                    wantsInvoice: !isFree.value && wantsInvoice.value,
                    invoiceName: isFree.value ? "" : invoiceName.value,
                    promoCode: appliedPromo.value?.code ?? "",
                    deliveryEmail: deliveryEmail.value.trim(),
                    accessPassword: accessPassword.value
                }
            });

            if (res.isSuccess && res.data?.success) {
                stopHoldTimer();
                holdActive.value = false;
                confirmation.value = res.data;
                step.value = 5;
            }
            else {
                // Fallo CONFIRMADO por el servidor (transaction == null): el hold quedó liberado.
                // Re-reservamos para que pueda reintentar; si el cupo se agotó, volvemos a Revisión.
                gatewayError.value = res.errorMessage || "No se pudo completar la compra.";
                // Detener el timer del hold viejo ANTES de re-reservar: si no, podría dispararse
                // onHoldExpired en paralelo durante el createHold y liberar el hold NUEVO.
                stopHoldTimer();
                holdActive.value = false;
                const reheld = await createHold();
                if (!reheld) {
                    gatewayControlModel.value = null;
                    step.value = 3;
                }
            }
        }
        catch {
            // Error de red/timeout: NO sabemos si el cobro se ejecutó en la pasarela. Por eso NO regeneramos
            // el PaymentReference ni re-reservamos (eso provocaría un SEGUNDO cobro con otro token). Mantenemos
            // el mismo intento; el usuario puede reintentar y la idempotencia del servidor (orden ya Paid por
            // PaymentReference) devuelve la confirmación sin volver a cobrar.
            gatewayError.value = "No pudimos confirmar tu pago. Si ya se procesó, recibirás tus entradas por correo. Puedes reintentar el pago en unos segundos.";
        }
        finally {
            busy.value = false;
        }
    }

    // #endregion

    // #region Gateway event handlers

    async function onPayClick(): Promise<void> {
        await paymentForm.value?.submitForm();
    }

    // Entrada gratuita: confirma sin pasarela ni token (el servidor valida que el total sea 0).
    async function confirmFree(): Promise<void> {
        gatewayError.value = "";
        await submitCheckout("");
    }

    function onPaymentFormSubmit(): Promise<void> {
        gatewayError.value = "";
        gatewayValidationFields.value = [];
        gatewayCompletionSource = new PromiseCompletionSource();
        // Dispara la tokenización en el GatewayControl; resuelve vía success/error/validation.
        submitGatewayPayment();
        return gatewayCompletionSource.promise;
    }

    async function onGatewaySuccess(token: string): Promise<void> {
        await submitCheckout(token);
        gatewayCompletionSource?.resolve();
    }

    function onGatewayError(message: string): void {
        gatewayError.value = message;
        gatewayCompletionSource?.resolve();
    }

    function onGatewayValidation(fields: FormError[]): void {
        gatewayValidationFields.value = fields;
        gatewayCompletionSource?.resolve();
    }

    // #endregion

    return {
        // Asistentes y preguntas (paso 2 + opciones de invitado): attendeeState.partial.ts
        ...attendee,

        // Config y constantes
        config,
        stepLabels,

        // Estado general del wizard
        step,
        busy,
        err,
        confirmation,

        // Paso 1: entradas
        ticketTypes,
        hasSelection,
        total,
        getQty,
        setQty,
        maxQtyFor,
        formatCurrency,
        formatShortDate,
        earlyBirdCountdown,
        reloadTicketTypes,
        goToAttendees,

        // Paso 2: navegación (el resto del paso viene de ...attendee)
        backToTickets,
        goToReview,

        // Paso 3: revisión (promo + factura/NIT)
        summaryRows,
        discount,
        grandTotal,
        isFree,
        ivaIncluded,
        promoInput,
        appliedPromo,
        promoBusy,
        promoError,
        applyPromo,
        removePromo,
        wantsInvoice,
        deliveryEmail,
        deliveryEmailValid,
        nit,
        invoiceName,
        nitAddress,
        busyNit,
        nitError,
        validateNit,
        goToPayment,

        // Paso 4: pago (hold + pasarela)
        holdActive,
        holdExpired,
        holdRemaining,
        holdCountdownLabel,
        holdProgressPct,
        gatewayControlModel,
        gatewayError,
        gatewayValidationFields,
        paymentForm,
        backToReview,
        confirmFree,
        onPayClick,
        onPaymentFormSubmit,
        onGatewaySuccess,
        onGatewayError,
        onGatewayValidation,

        // Paso 5: confirmación
        pdfBusy,
        downloadPdf,
        walletKind,
        walletBusy,
        walletError,
        addToWallet,
        goHome,

        // Shell (hero, progreso, evento finalizado)
        event,
        slimHeader,
        eventEnded,
        categorySlug,
        eventSubtitle,
        eventDateLabel,
        eventDateRange,
        progressPct,

        // Gate de contraseña (visibilidad "Con contraseña")
        passwordRequired,
        accessPassword,
        unlockBusy,
        unlockError,
        unlockEvent
    };
}

/** Estado completo del checkout (tipado por inferencia: destructurar un miembro inexistente no compila). */
export type CheckoutState = ReturnType<typeof createCheckoutState>;

const checkoutStateKey: InjectionKey<CheckoutState> = Symbol("EventCheckoutState");

/** Crea el estado y lo provee a los partials de paso. SOLO el shell lo llama. */
export function provideCheckoutState(): CheckoutState {
    const state = createCheckoutState();
    provide(checkoutStateKey, state);
    return state;
}

/** Inyecta el estado provisto por el shell. Cada partial de paso lo llama en su setup. */
export function useCheckoutState(): CheckoutState {
    const state = inject(checkoutStateKey);
    if (!state) {
        throw new Error("EventCheckout: el estado no fue provisto (useCheckoutState fuera del shell).");
    }
    return state;
}
