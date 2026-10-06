// Sub-estado de asistentes y preguntas del checkout: unidades por entrada (familia / invitado),
// preguntas del tipo de boleto (básicos + atributos del catálogo), prefill desde el perfil,
// validación por unidad, acordeón, y el armado de las líneas del payload (buildLines).
// Lo crea checkoutState.partial.ts pasándole sus dependencias; no se usa directo desde partials.
import { computed, ref, type Ref } from "vue";
import { PublicAttributeBag } from "@Obsidian/ViewModels/Utility/publicAttributeBag";
import { ListItemBag } from "@Obsidian/ViewModels/Utility/listItemBag";
import {
    AttendeeAnswersBag,
    AttendeeOptionBag,
    AttendeeUnit,
    CheckoutLineBag,
    InitBag,
    QuestionDefBag,
    TicketTypeBag,
    guestValue
} from "./types.partial";

type InvokeBlockActionFn = <T>(actionName: string, data?: Record<string, unknown>) => Promise<{
    isSuccess: boolean;
    data: T | null;
    errorMessage: string | null;
}>;

type AttendeeStateDeps = {
    config: InitBag;
    invokeBlockAction: InvokeBlockActionFn;
    ticketTypes: Ref<TicketTypeBag[]>;
    getQty: (ticketTypeId: number) => number;
};

// eslint-disable-next-line @typescript-eslint/explicit-function-return-type
export function createAttendeeState(deps: AttendeeStateDeps) {
    const { config, invokeBlockAction, ticketTypes, getQty } = deps;

    const attendeeUnits = ref<AttendeeUnit[]>([]);
    const familyMembers = ref<AttendeeOptionBag[]>(config.buyer ? [config.buyer] : []);

    // La misma persona no puede tener dos entradas del mismo tipo de boleto: cada dropdown
    // excluye a los ya asignados en las OTRAS unidades de ese tipo (el servidor también lo valida).
    function choicesFor(unit: AttendeeUnit): ListItemBag[] {
        const taken = new Set(attendeeUnits.value
            .filter(u => u.key !== unit.key && u.ticketTypeId === unit.ticketTypeId && u.selection !== guestValue)
            .map(u => u.selection));
        const items: ListItemBag[] = familyMembers.value
            .filter(m => m.personAliasId != null && !taken.has(String(m.personAliasId)))
            .map(m => ({ value: String(m.personAliasId), text: m.name ?? "" }));
        items.push({ value: guestValue, text: "Otro invitado..." });
        return items;
    }

    const genderOptions: ListItemBag[] = [
        { value: "M", text: "Masculino" },
        { value: "F", text: "Femenino" }
    ];

    // Roles de Known Relationship ("¿qué relación tiene contigo?") — vienen del servidor.
    const relationRoles: ListItemBag[] = config.relationRoles ?? [];

    // ---------- Selector de fecha de nacimiento propio (Día / Mes / Año, en español) ----------
    const MONTHS_ES = ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];
    /**
     * Días del mes elegido. Con 1-31 fijo se podía guardar "31 de febrero": el combo la ofrecía y
     * nada la rechazaba después. Sin mes todavía elegido se ofrecen 31; sin año se asume bisiesto
     * para no esconder el 29 de febrero antes de que elijan el año.
     */
    function daysInMonth(month: number, year: number): number {
        if (!month) {
            return 31;
        }
        return new Date(year || 2000, month, 0).getDate();
    }

    function dobDayOptionsFor(unit: AttendeeUnit): ListItemBag[] {
        const total = daysInMonth(Number(unit.dobParts.m), Number(unit.dobParts.y));
        return Array.from({ length: total }, (_, i) => ({ value: String(i + 1), text: String(i + 1) }));
    }

    // ---------- Validación de formato ----------
    // El servidor vuelve a validar; esto existe para que el error aparezca donde el usuario lo
    // puede corregir y no al final del checkout (o peor: nunca, y entre basura a la base).
    const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[A-Za-z]{2,}$/;

    function emailError(value: string): string {
        const text = (value ?? "").trim();
        if (!text) {
            return "";
        }
        return EMAIL_RE.test(text) ? "" : "Escribe un correo válido (ejemplo: nombre@dominio.com).";
    }

    function phoneError(value: string): string {
        const text = (value ?? "").trim();
        if (!text) {
            return "";
        }
        if (/[A-Za-zÁÉÍÓÚÜÑáéíóúüñ]/.test(text)) {
            return "El teléfono solo lleva números.";
        }
        return text.replace(/\D/g, "").length >= 8 ? "" : "El teléfono debe tener al menos 8 dígitos.";
    }

    function dobError(unit: AttendeeUnit): string {
        const { d, m, y } = unit.dobParts;
        if (!d && !m && !y) {
            return "";
        }
        if (!d || !m || !y) {
            return "Completa día, mes y año.";
        }
        const max = daysInMonth(Number(m), Number(y));
        if (Number(d) > max) {
            return `${MONTHS_ES[Number(m) - 1]} de ${y} tiene ${max} días.`;
        }
        const picked = new Date(Number(y), Number(m) - 1, Number(d));
        const today = new Date();
        today.setHours(0, 0, 0, 0);
        return picked.getTime() > today.getTime() ? "La fecha de nacimiento no puede ser futura." : "";
    }
    const dobMonthOptions: ListItemBag[] = MONTHS_ES.map((m, i) => ({ value: String(i + 1), text: m }));
    const dobYearOptions: ListItemBag[] = Array.from({ length: new Date().getFullYear() - 1919 }, (_, i) => {
        const y = new Date().getFullYear() - i;
        return { value: String(y), text: String(y) };
    });

    function setDobPart(unit: AttendeeUnit, part: "d" | "m" | "y", value: string): void {
        unit.dobParts[part] = value;

        // Al cambiar mes o año, un día que dejó de existir (31 en un mes de 30) se recorta en vez
        // de quedarse guardado como fecha imposible.
        const max = daysInMonth(Number(unit.dobParts.m), Number(unit.dobParts.y));
        if (Number(unit.dobParts.d) > max) {
            unit.dobParts.d = String(max);
        }

        const { d, m, y } = unit.dobParts;
        unit.answers.birthDate = (d && m && y && !dobError(unit))
            ? `${y}-${m.padStart(2, "0")}-${d.padStart(2, "0")}`
            : null;
    }

    function dobPartsFromIso(iso: string | null): { d: string; m: string; y: string } {
        const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso ?? "");
        if (!match) {
            return { d: "", m: "", y: "" };
        }
        return { y: match[1], m: String(Number(match[2])), d: String(Number(match[3])) };
    }

    function questionsFor(unit: AttendeeUnit): QuestionDefBag[] {
        return ticketTypes.value.find(t => t.id === unit.ticketTypeId)?.questions ?? [];
    }

    function attrBagsFor(unit: AttendeeUnit): Record<string, PublicAttributeBag> {
        const map: Record<string, PublicAttributeBag> = {};
        for (const q of questionsFor(unit)) {
            if (q.kind === "attr" && q.attribute?.key) {
                map[q.attribute.key] = q.attribute;
            }
        }
        return map;
    }

    function hasAttrQuestions(unit: AttendeeUnit): boolean {
        return questionsFor(unit).some(q => q.kind === "attr" && q.attribute?.key);
    }

    // guid -> key de atributo, sobre todas las preguntas del evento (para mapear el prefill).
    const attrKeyByGuid = computed<Record<string, string>>(() => {
        const map: Record<string, string> = {};
        for (const tt of ticketTypes.value) {
            for (const q of tt.questions ?? []) {
                if (q.kind === "attr" && q.attributeGuid && q.attribute?.key) {
                    map[q.attributeGuid.toLowerCase()] = q.attribute.key;
                }
            }
        }
        return map;
    });

    function answerValueFor(unit: AttendeeUnit, q: QuestionDefBag): string {
        if (q.kind === "basic") {
            switch (q.key) {
                case "phone": return unit.answers.phone;
                case "email": return unit.answers.email;
                case "birthDate": return unit.answers.birthDate ?? "";
                case "gender": return unit.answers.gender;
            }
            return "";
        }
        return unit.attrValues[q.attribute?.key ?? ""] ?? "";
    }

    // Prefill: las respuestas que la persona ya tiene (perfil + atributos del catálogo).
    const answersCache = new Map<number, AttendeeAnswersBag>();

    async function prefillUnit(unit: AttendeeUnit): Promise<void> {
        if (unit.selection === guestValue || !questionsFor(unit).length) {
            return;
        }
        const aliasId = Number(unit.selection);
        if (!aliasId) {
            return;
        }

        let a = answersCache.get(aliasId);
        if (!a) {
            try {
                const res = await invokeBlockAction<AttendeeAnswersBag>("GetAttendeeAnswers", { personAliasId: aliasId });
                if (res.isSuccess && res.data) {
                    a = res.data;
                    answersCache.set(aliasId, a);
                }
            } catch { /* prefill es best-effort */ }
        }
        if (!a || unit.selection !== String(aliasId)) {
            return; // el usuario cambió la selección mientras cargaba
        }

        unit.answers = {
            phone: a.phone ?? "",
            email: a.email ?? "",
            birthDate: a.birthDate || null,
            gender: a.gender ?? ""
        };
        unit.dobParts = dobPartsFromIso(a.birthDate ?? null);
        const attrValues: Record<string, string> = {};
        for (const [guid, val] of Object.entries(a.attrs ?? {})) {
            const key = attrKeyByGuid.value[guid.toLowerCase()];
            if (key) {
                attrValues[key] = val ?? "";
            }
        }
        unit.attrValues = attrValues;
    }

    function blankUnitAnswers(unit: AttendeeUnit): void {
        unit.answers = { phone: "", email: "", birthDate: null, gender: "" };
        unit.dobParts = { d: "", m: "", y: "" };
        unit.attrValues = {};
    }

    function unitValid(u: AttendeeUnit): boolean {
        if (u.selection === guestValue) {
            // Invitado nuevo: nombre, apellido, fecha de nacimiento, sexo y relación son obligatorios
            // (con esos datos el servidor crea la persona real y la enlaza al comprador).
            // Mínimo 2 caracteres: mismo umbral que el servidor (evita fallar hasta el paso de pago).
            if (u.guestFirstName.trim().length < 2 || u.guestLastName.trim().length < 2) return false;
            if (!u.answers.birthDate) return false;
            if (!u.answers.gender) return false;
            // La relación solo se exige si hay roles disponibles (sin roles, la compra no se bloquea).
            if (relationRoles.length > 0 && !u.guestRelation) return false;
        }
        else if (u.selection.length === 0) {
            return false;
        }
        else if (attendeeUnits.value.some(o => o.key !== u.key && o.ticketTypeId === u.ticketTypeId && o.selection === u.selection)) {
            // Duplicado: la misma persona dos veces en el mismo tipo de boleto.
            return false;
        }
        // Formato: un correo o teléfono con basura no puede pasar como "✓ Completo".
        if (emailError(u.answers.email) || phoneError(u.answers.phone) || dobError(u)) {
            return false;
        }
        // Preguntas obligatorias respondidas.
        return questionsFor(u).every(q => !q.required || answerValueFor(u, q).trim().length > 0);
    }

    const attendeesValid = computed(() => attendeeUnits.value.every(unitValid));

    // ---------- Acordeón entre tickets ----------
    // Con preguntas y varias entradas, el paso se vuelve un formulario largo: se muestra
    // UN asistente a la vez (los demás colapsados con resumen y estado) y se navega entre ellos.
    const openUnitKey = ref<string>("");

    const accordionMode = computed(() =>
        attendeeUnits.value.length > 1
        && attendeeUnits.value.some(u => questionsFor(u).length > 0 || u.selection === guestValue));

    function toggleUnit(unit: AttendeeUnit): void {
        openUnitKey.value = openUnitKey.value === unit.key ? "" : unit.key;
    }

    function nextUnitAfter(unit: AttendeeUnit): AttendeeUnit | null {
        const idx = attendeeUnits.value.findIndex(u => u.key === unit.key);
        return idx >= 0 && idx + 1 < attendeeUnits.value.length ? attendeeUnits.value[idx + 1] : null;
    }

    function goNextUnit(unit: AttendeeUnit): void {
        // Saltar con la tarjeta incompleta la colapsaba sin avisar y dejaba el error escondido
        // hasta el final; el botón se deshabilita mientras falte algo.
        if (!unitValid(unit)) {
            return;
        }
        const next = nextUnitAfter(unit);
        if (next) {
            openUnitKey.value = next.key;
        }
    }

    function attendeeDisplayName(unit: AttendeeUnit): string {
        if (unit.selection === guestValue) {
            const full = `${unit.guestFirstName.trim()} ${unit.guestLastName.trim()}`.trim();
            return full || "Invitado sin nombre";
        }
        const member = familyMembers.value.find(m => String(m.personAliasId) === unit.selection);
        return member?.name ?? "Asistente";
    }

    async function loadFamilyMembers(): Promise<void> {
        try {
            const res = await invokeBlockAction<{ members: AttendeeOptionBag[] }>("GetFamilyMembers", {});
            if (res.isSuccess && res.data?.members?.length) {
                familyMembers.value = res.data.members;
            }
        }
        catch {
            // Silencioso: si falla, el comprador siempre está como opción.
        }
    }

    function buildAttendeeUnits(): void {
        const units: AttendeeUnit[] = [];
        const newUnits: AttendeeUnit[] = [];
        const buyerValue = config.buyer?.personAliasId != null ? String(config.buyer.personAliasId) : guestValue;

        // Conserva lo ya tecleado: al volver del paso 1 (subir/bajar cantidades) las unidades
        // que siguen existiendo (misma key tipo-índice) se reusan con todo su estado.
        const prev = new Map(attendeeUnits.value.map(u => [u.key, u]));

        for (const tt of ticketTypes.value) {
            const qty = getQty(tt.id);
            for (let i = 0; i < qty; i++) {
                const key = `${tt.id}-${i}`;
                const existing = prev.get(key);
                if (existing) {
                    units.push(existing);
                    continue;
                }
                const unit: AttendeeUnit = {
                    key,
                    ticketTypeId: tt.id,
                    ticketTypeName: tt.name,
                    indexInType: i + 1,
                    // El primero por defecto es el comprador; el resto, invitado.
                    selection: i === 0 ? buyerValue : guestValue,
                    guestName: "",
                    guestFirstName: "",
                    guestLastName: "",
                    guestRelation: "",
                    answers: { phone: "", email: "", birthDate: null, gender: "" },
                    dobParts: { d: "", m: "", y: "" },
                    attrValues: {}
                };
                units.push(unit);
                newUnits.push(unit);
            }
        }
        attendeeUnits.value = units;
        openUnitKey.value = units[0]?.key ?? "";

        // Prefill SOLO de las unidades nuevas con persona (las reusadas conservan ediciones).
        // OJO: iterar attendeeUnits.value (proxies reactivos), NO los objetos crudos de newUnits —
        // prefillUnit muta la unidad al responder el servidor, y mutar el crudo no dispara
        // reactividad (los datos llegaban pero la UI no repintaba hasta re-seleccionar).
        const newKeys = new Set(newUnits.map(u => u.key));
        for (const u of attendeeUnits.value) {
            if (newKeys.has(u.key) && u.selection !== guestValue) {
                void prefillUnit(u);
            }
        }
    }

    function onAttendeeSelect(unit: AttendeeUnit, value: string): void {
        unit.selection = value;
        if (value !== guestValue) {
            unit.guestName = "";
            unit.guestFirstName = "";
            unit.guestLastName = "";
            unit.guestRelation = "";
            void prefillUnit(unit);
        }
        else {
            blankUnitAnswers(unit);
        }
    }

    function buildLines(): CheckoutLineBag[] {
        const lines: CheckoutLineBag[] = [];
        for (const tt of ticketTypes.value) {
            const qty = getQty(tt.id);
            if (qty <= 0) {
                continue;
            }
            const units = attendeeUnits.value.filter(u => u.ticketTypeId === tt.id);
            const attendees: AttendeeOptionBag[] = units.map(u => {
                // Respuestas: atributos se mandan por GUID (el server los valida contra el catálogo).
                const attrs: Record<string, string> = {};
                for (const q of questionsFor(u)) {
                    if (q.kind === "attr" && q.attributeGuid && q.attribute?.key) {
                        attrs[q.attributeGuid] = u.attrValues[q.attribute.key] ?? "";
                    }
                }
                const answers: AttendeeAnswersBag = {
                    phone: u.answers.phone,
                    email: u.answers.email,
                    birthDate: u.answers.birthDate ?? "",
                    gender: u.answers.gender,
                    attrs
                };

                if (u.selection === guestValue) {
                    const first = u.guestFirstName.trim();
                    const last = u.guestLastName.trim();
                    return {
                        personAliasId: null,
                        name: `${first} ${last}`.trim() || u.guestName.trim(),
                        firstName: first,
                        lastName: last,
                        relationRoleId: u.guestRelation ? Number(u.guestRelation) : null,
                        answers
                    };
                }
                const member = familyMembers.value.find(m => String(m.personAliasId) === u.selection);
                return { personAliasId: Number(u.selection), name: member?.name ?? null, answers };
            });
            lines.push({ ticketTypeId: tt.id, quantity: qty, attendees });
        }
        return lines;
    }

    return {
        attendeeUnits,
        familyMembers,
        attendeesValid,
        accordionMode,
        openUnitKey,
        genderOptions,
        relationRoles,
        dobDayOptionsFor,
        dobMonthOptions,
        dobYearOptions,
        choicesFor,
        onAttendeeSelect,
        setDobPart,
        questionsFor,
        attrBagsFor,
        hasAttrQuestions,
        unitValid,
        emailError,
        phoneError,
        dobError,
        toggleUnit,
        nextUnitAfter,
        goNextUnit,
        attendeeDisplayName,
        loadFamilyMembers,
        buildAttendeeUnits,
        buildLines
    };
}
