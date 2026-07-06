// Tipos del checkout de eventos (espejo de los bags C# en Rock.ViewModels.Blocks.Eventos.EventCheckout).
import { PublicAttributeBag } from "@Obsidian/ViewModels/Utility/publicAttributeBag";
import { ListItemBag } from "@Obsidian/ViewModels/Utility/listItemBag";

export type EventBag = {
    id: number;
    guid: string;
    name: string;
    slug?: string | null;
    description?: string | null;
    startDateTime: string;
    endDateTime: string;
    venueName?: string | null;
    campusName?: string | null;
    imageUrl?: string | null;
    organizerName?: string | null;
    headerStyle?: string | null;
    category?: string | null;
    sessions?: string[] | null;
};

export type TicketTypeBag = {
    id: number;
    name: string;
    description?: string | null;
    price: number;
    effectivePrice: number;
    isEarlyBird: boolean;
    earlyBirdUntil?: string | null;
    capacity?: number | null;
    remaining?: number | null;
    soldOut: boolean;
    maxPerOrder?: number | null;
    onSale: boolean;
    sortOrder: number;
    questions?: QuestionDefBag[] | null;
};

export type QuestionDefBag = {
    kind: string;               // "basic" | "attr"
    key?: string | null;        // basic: phone | email | birthDate | gender
    required: boolean;
    attributeGuid?: string | null;
    attribute?: PublicAttributeBag | null;
};

export type AttendeeAnswersBag = {
    phone?: string | null;
    email?: string | null;
    birthDate?: string | null;
    gender?: string | null;
    attrs?: Record<string, string> | null;   // guid -> valor (formato público de edición)
};

export type AttendeeOptionBag = {
    personAliasId?: number | null;
    name?: string | null;
    isCurrentPerson?: boolean;
    answers?: AttendeeAnswersBag | null;
    firstName?: string | null;
    lastName?: string | null;
    relationRoleId?: number | null;
};

export type InitBag = {
    notLogged: boolean;
    eventFound: boolean;
    event?: EventBag | null;
    ticketTypes?: TicketTypeBag[] | null;
    buyer?: AttendeeOptionBag | null;
    hasGateway: boolean;
    relationRoles?: ListItemBag[] | null;
    currentPersonEmail?: string | null;
    requiresPassword?: boolean;
    calendarUrl?: string | null;
};

export type ConfirmationTicketBag = {
    uniqueCode: string;
    ticketTypeName: string;
    attendeeName?: string | null;
    pricePaid: number;
    qrImageDataUri?: string | null;
};

export type ProcessCheckoutResponseBag = {
    success: boolean;
    orderId: number;
    status: string;
    total: number;
    subtotal: number;
    discountTotal: number;
    /** Recargo por pago en cuotas (FeeCoverage de la transacción). */
    surcharge: number;
    /** Monto realmente cobrado (incluye recargo); = total si pagó al contado. */
    amountCharged: number;
    paymentReference: string;
    tickets: ConfirmationTicketBag[];
};

export type CheckoutLineBag = {
    ticketTypeId: number;
    quantity: number;
    attendees: AttendeeOptionBag[];
};

export type ApplyPromoResponseBag = {
    code: string;
    discountTotal: number;
    description: string;
    newTotal: number;
};

export type CreateHoldResponseBag = {
    orderId: number;
    paymentReference: string;
    expiresDateTime: string;
    holdSeconds: number;
    subtotal: number;
    total: number;
};

export type AttendeeUnit = {
    key: string;
    ticketTypeId: number;
    ticketTypeName: string;
    indexInType: number;
    selection: string;     // guestValue, or personAliasId as string
    guestName: string;
    // Invitado nuevo: se convierte en persona real en el servidor (known relationship).
    guestFirstName: string;
    guestLastName: string;
    guestRelation: string; // role id del Known Relationship, como string (DropDownList)
    // Respuestas a las preguntas del tipo de boleto (básicos + atributos por KEY de atributo).
    answers: { phone: string; email: string; birthDate: string | null; gender: string };
    // Partes del selector de fecha de nacimiento propio (Día/Mes/Año en español).
    dobParts: { d: string; m: string; y: string };
    attrValues: Record<string, string>;
};

/** Valor centinela del dropdown de asistente para "Otro invitado...". */
export const guestValue = "guest";
