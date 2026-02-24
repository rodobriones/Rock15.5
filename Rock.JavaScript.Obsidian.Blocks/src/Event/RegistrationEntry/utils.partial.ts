// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//

import { Guid } from "@Obsidian/Types";
import { CurrentPersonBag } from "@Obsidian/ViewModels/Crm/currentPersonBag";
import { newGuid } from "@Obsidian/Utility/guid";
import {
    RegistrationEntryState,
    RegistrationCostSummaryInfo,
    RegistrantBasicInfo,
    PaymentPlanConfiguration,
    PersonGuid,
    FormFieldGuid,
    FormFieldValue,
    RegistrationEntryTerminology
} from "./types.partial";
import { ComputedRef, InjectionKey, Ref, inject, nextTick } from "vue";
import { smoothScrollToTop } from "@Obsidian/Utility/page";
import { PublicComparisonValueBag } from "@Obsidian/ViewModels/Utility/publicComparisonValueBag";
import { ComparisonValue } from "@Obsidian/Types/Reporting/comparisonValue";
import { RegistrationEntryArgsBag } from "@Obsidian/ViewModels/Blocks/Event/RegistrationEntry/registrationEntryArgsBag";
import { RegistrantsSameFamily } from "@Obsidian/Enums/Event/registrantsSameFamily";
import { RegistrationPersonFieldType } from "@Obsidian/Enums/Event/registrationPersonFieldType";
import { RegistrationFieldSource } from "@Obsidian/Enums/Event/registrationFieldSource";
import { RegistrantBag } from "@Obsidian/ViewModels/Blocks/Event/RegistrationEntry/registrantBag";
import { RegistrationEntryFormBag } from "@Obsidian/ViewModels/Blocks/Event/RegistrationEntry/registrationEntryFormBag";
import { RegistrationEntryFormFieldBag } from "@Obsidian/ViewModels/Blocks/Event/RegistrationEntry/registrationEntryFormFieldBag";
import { RegistrationEntryInitializationBox } from "@Obsidian/ViewModels/Blocks/Event/RegistrationEntry/registrationEntryInitializationBox";

/** If all registrants are to be in the same family, but there is no currently authenticated person,
 *  then this guid is used as a common family guid */
const unknownSingleFamilyGuid = newGuid();

/**
 * If there is a forced family guid because of RegistrantsSameFamily setting, then this returns that guid
 * @param currentPerson
 * @param viewModel
 */
export function getForcedFamilyGuid(currentPerson: CurrentPersonBag | null, viewModel: RegistrationEntryInitializationBox): string | null {
    return (currentPerson && viewModel.registrantsSameFamily === RegistrantsSameFamily.Yes) ?
        (viewModel.currentPersonFamilyGuid || unknownSingleFamilyGuid) :
        null;
}

/**
 * Get a default registrant object with the current family guid set.
 * @param currentPerson
 * @param viewModel
 * @param familyGuid
 */
export function getDefaultRegistrantInfo(currentPerson: CurrentPersonBag | null, viewModel: RegistrationEntryInitializationBox, familyGuid: Guid | null): RegistrantBag {
    const forcedFamilyGuid = getForcedFamilyGuid(currentPerson, viewModel);

    if (forcedFamilyGuid) {
        familyGuid = forcedFamilyGuid;
    }

    // If the family is not specified, then assume the person is in their own family.
    if (!familyGuid && viewModel.registrantsSameFamily === RegistrantsSameFamily.No) {
        familyGuid = newGuid();
    }

    const registrantBag: RegistrantBag = {
        cost: 0,
        isOnWaitList: false,
        familyGuid: familyGuid,
        fieldValues: {},
        feeItemQuantities: {},
        guid: newGuid(),
        personGuid: null,
        isNewFamily: false
    };

    return registrantBag;
}

export function getRegistrantBasicInfo(registrant: RegistrantBag, registrantForms: RegistrationEntryFormBag[]): RegistrantBasicInfo {
    // TODO Should Guids here be enforced?
    const fields = registrantForms?.reduce((acc, f) => acc.concat(f.fields ?? []), [] as RegistrationEntryFormFieldBag[]) || [];

    const firstNameGuidOrEmptyString = fields.find(f => f.personFieldType === RegistrationPersonFieldType.FirstName && f.fieldSource === RegistrationFieldSource.PersonField)?.guid || "";
    const lastNameGuidOrEmptyString = fields.find(f => f.personFieldType === RegistrationPersonFieldType.LastName && f.fieldSource === RegistrationFieldSource.PersonField)?.guid || "";
    const emailGuidOrEmptyString = fields.find(f => f.personFieldType === RegistrationPersonFieldType.Email && f.fieldSource === RegistrationFieldSource.PersonField)?.guid || "";

    return {
        firstName: (registrant?.fieldValues?.[firstNameGuidOrEmptyString] || "") as string,
        lastName: (registrant?.fieldValues?.[lastNameGuidOrEmptyString] || "") as string,
        email: (registrant?.fieldValues?.[emailGuidOrEmptyString] || "") as string,
        guid: registrant?.guid || "",
        isOnWaitList: registrant.isOnWaitList
    };
}

/** Scrolls to the top of the window after the next render. */
export function scrollToTopAfterNextRender(): void {
    nextTick(() => smoothScrollToTop());
}

/**
 * Injects a provided value.
 * Throws an exception if the value is undefined or not yet provided.
 */
export function use<T>(key: string | InjectionKey<T>): T {
    const result = inject<T>(key);

    if (result === undefined) {
        throw `Attempted to access ${key} before a value was provided.`;
    }

    return result;
}

export function convertComparisonValue(value: PublicComparisonValueBag): ComparisonValue {
    return {
        value: value.value ?? "",
        comparisonType: value.comparisonType
    };
}

const registrationEntryText: Record<string, { en: string; es: string; }> = {
    actionApply: { en: "Apply", es: "Aplicar" },
    actionClose: { en: "Close", es: "Cerrar" },
    actionCancel: { en: "Cancel", es: "Cancelar" },
    actionChange: { en: "Change", es: "Cambiar" },
    actionDelete: { en: "Delete", es: "Eliminar" },
    actionFinish: { en: "Finish", es: "Finalizar" },
    actionNext: { en: "Next", es: "Siguiente" },
    actionPay: { en: "Pay", es: "Pagar" },
    actionPrevious: { en: "Previous", es: "Anterior" },
    actionRequestExtension: { en: "Request Extension", es: "Solicitar extension" },
    actionYes: { en: "Yes", es: "Si" },
    amountDue: { en: "Amount Due", es: "Monto adeudado" },
    amountToPayToday: { en: "Amount To Pay Today", es: "Monto a pagar hoy" },
    labelAmountDueToday: { en: "Amount Due Today", es: "Monto adeudado hoy" },
    amountRemainingAfterPayment: { en: "Amount Remaining After Payment", es: "Saldo restante despues del pago" },
    discountCodeInvalid: { en: "'{code}' is not a valid {term}.", es: "'{code}' no es un {term} valido." },
    discountedAmount: { en: "Discounted Amount", es: "Monto con descuento" },
    durationManyMinutes: { en: "{minutes} minutes", es: "{minutes} minutos" },
    durationOneMinute: { en: "a minute", es: "un minuto" },
    headingHowManyRegistrants: { en: "How many {registrants} will you be registering?", es: "Cuantas {registrants} vas a registrar?" },
    headingIncorrectConfiguration: { en: "Incorrect Configuration", es: "Configuracion incorrecta" },
    headingMakePaymentsEvenEasier: { en: "Make Payments Even Easier", es: "Haz tus pagos mas faciles" },
    headingPaymentInformation: { en: "Payment Information", es: "Informacion de pago" },
    headingPaymentSummary: { en: "Payment Summary", es: "Resumen de pago" },
    headingPleaseCorrectTheFollowing: { en: "Please correct the following:", es: "Por favor corrige lo siguiente:" },
    headingPleaseLogIn: { en: "Please log in", es: "Inicia sesion por favor" },
    headingPleaseSignDocumentFor: { en: "Please Sign the {document} for {name}", es: "Firma la {document} para {name}" },
    headingSorry: { en: "Sorry", es: "Lo sentimos" },
    headingThisRegistrationWasCompletedBy: { en: "This {registration} Was Completed By", es: "Este {registration} fue completado por" },
    headingTimedOut: { en: "{registration} Timed Out", es: "{registration} expiro" },
    headingRegistrationFull: { en: "{registration} Full", es: "{registration} Lleno" },
    itemNotEligibleForDiscount: { en: "This item is not eligible for the discount.", es: "Este articulo no es elegible para el descuento." },
    labelAddress: { en: "Address", es: "Direccion" },
    labelAmount: { en: "Amount", es: "Monto" },
    labelAnniversaryDate: { en: "Anniversary Date", es: "Fecha de aniversario" },
    labelBirthDate: { en: "Birth Date", es: "Fecha de nacimiento" },
    labelCampus: { en: "Campus", es: "Campus" },
    labelConnectionStatus: { en: "Connection Status", es: "Estado de conexion" },
    labelDescription: { en: "Description", es: "Descripcion" },
    labelEmail: { en: "Email", es: "Correo electronico" },
    labelEthnicity: { en: "Ethnicity", es: "Origen etnico" },
    labelFamily: { en: "Family", es: "Familia" },
    labelFamilyMemberToRegister: { en: "Family Member to Register", es: "Familiar a registrar" },
    labelFirstName: { en: "First Name", es: "Nombre" },
    labelGender: { en: "Gender", es: "Genero" },
    labelGrade: { en: "Grade", es: "Grado" },
    labelGuestToRegister: { en: "Guest to Register", es: "Invitado a registrar" },
    labelHomePhone: { en: "Home Phone", es: "Telefono particular" },
    labelImmediateFamily: { en: "immediate family", es: "familia inmediata" },
    labelIsInSameFamilyAs: { en: "{person} is in the same {familyTerm} as", es: "{person} esta en la misma {familyTerm} que" },
    labelLastName: { en: "Last Name", es: "Apellido" },
    labelLanguage: { en: "Language", es: "Idioma" },
    labelMaritalStatus: { en: "Marital Status", es: "Estado civil" },
    labelMiddleName: { en: "Middle Name", es: "Segundo nombre" },
    labelMinimumDueToday: { en: "Minimum Due Today", es: "Minimo a pagar hoy" },
    labelMobilePhone: { en: "Mobile Phone", es: "Telefono movil" },
    labelNoCancelRegistration: { en: "No, cancel {registration}", es: "No, cancelar {registration}" },
    labelNumberOfPayments: { en: "Number of Payments", es: "Numero de pagos" },
    labelNone: { en: "None", es: "Ninguno" },
    labelNoneOfTheAbove: { en: "None of the above", es: "Ninguno de los anteriores" },
    labelNewFamilyMember: { en: "New Family Member", es: "Nuevo familiar" },
    labelPaymentPlan: { en: "Payment Plan", es: "Plan de pagos" },
    labelPayInFull: { en: "pay in full", es: "pago completo" },
    labelNewPaymentMethod: { en: "New Payment Method", es: "Nuevo metodo de pago" },
    labelPaymentAmount: { en: "Payment Amount", es: "Monto del pago" },
    labelPaymentSchedule: { en: "Payment Schedule", es: "Calendario de pagos" },
    labelPlanSummary: { en: "Plan Summary", es: "Resumen del plan" },
    labelPreviouslyPaid: { en: "Previously Paid", es: "Pagado anteriormente" },
    labelRace: { en: "Race", es: "Raza" },
    labelReviewRegistration: { en: "Review Registration", es: "Revisar registro" },
    labelSendConfirmationEmailsTo: { en: "Send Confirmation Emails To", es: "Enviar confirmaciones a" },
    labelShouldYourAccountBeUpdatedToUseThisEmailAddress: { en: "Should Your Account Be Updated To Use This Email Address?", es: "Deseas actualizar tu cuenta con este correo?" },
    labelSaveAccountForFuturePayments: { en: "Save account information for future payments", es: "Guardar metodo de pago para futuros pagos" },
    labelSelectPaymentFrequency: { en: "Select Your Desired Payment Frequency", es: "Selecciona tu frecuencia de pago" },
    labelStartDate: { en: "Start Date", es: "Fecha de inicio" },
    labelUnfortunately: { en: "Unfortunately", es: "Lamentablemente" },
    labelTimeLeftBeforeTimeout: { en: "Time left before timeout", es: "Tiempo restante antes de expirar" },
    labelTotal: { en: "Total", es: "Total" },
    labelTotalCost: { en: "Total Cost", es: "Costo total" },
    labelWorkPhone: { en: "Work Phone", es: "Telefono del trabajo" },
    messageAmount: { en: "Amount:", es: "Monto:" },
    messageAmountAdjustedForEvenDistribution: { en: "added {amount} to evenly distribute the remaining payments", es: "se agregaron {amount} para distribuir uniformemente los pagos restantes" },
    messageDiscountedAmount: { en: "Discounted Amount:", es: "Monto con descuento:" },
    messageDiscountCodeAppliedToMax: {
        en: "Your {discountText} {discountCodeTerm} was successfully applied to the maximum allowed number of {maxRegistrants} {registrantTerm}.",
        es: "Tu {discountCodeTerm} de {discountText} se aplico correctamente al maximo permitido de {maxRegistrants} {registrantTerm}."
    },
    messageDiscountCodeAppliedAll: {
        en: "Your {discountText} {discountCodeTerm} for all {registrantPluralTerm} was successfully applied.",
        es: "Tu {discountCodeTerm} de {discountText} para todas las {registrantPluralTerm} se aplico correctamente."
    },
    messageFieldIsRequired: { en: "is required", es: "es requerido" },
    messageSetUpPaymentPlan: { en: "Set Up Payment Plan", es: "Configurar plan de pagos" },
    messageDeletePaymentPlanConfirmation: { en: "Are you sure you want to delete this payment plan?", es: "Seguro que deseas eliminar este plan de pagos?" },
    messageNoPaymentPlanToDelete: { en: "An unknown error occurred while attempting to delete the payment plan.", es: "Ocurrio un error al intentar eliminar el plan de pagos." },
    messageOneMore: { en: "1 more {registrant}", es: "1 {registrant} mas" },
    messageManyMore: { en: "{count} more {registrants}", es: "{count} {registrants} mas" },
    messageMinimumDueToday: { en: "A minimum of {amount} is due today.", es: "Un minimo de {amount} debe pagarse hoy." },
    messageNoCancelRegistration: { en: "No, cancel {registration}", es: "No, cancelar {registration}" },
    messageNoMoreRegistrations: { en: "There are not any more {registrations} available for {instanceName}.", es: "Ya no hay {registrations} disponibles para {instanceName}." },
    messageNotFound: { en: "The selected registration could not be found or is no longer active.", es: "El registro seleccionado no se encuentra o ya no esta activo." },
    messageNotAllowedToView: { en: "You are not allowed to view or edit the selected {registration} since you are not the one who created the {registration}.", es: "No tienes permiso para ver o editar el {registration} seleccionado ya que no lo creaste." },
    messageMustBeLoggedIn: { en: "You must be logged in to access this {registration}.", es: "Debes iniciar sesion para acceder a este {registration}." },
    messageGatewayNotSupported: { en: "This {registration} has costs/{fees} associated with it but the configured payment gateway is not supported.", es: "Este {registration} tiene costos/{fees} asociados, pero la pasarela de pago configurada no es compatible." },
    messageInvalidState: { en: "Invalid State: '{state}'", es: "Estado invalido: '{state}'" },
    messageRegistrationNoCapacity: { en: "No payment was processed. Due to high demand there is no longer space available for this {registration}.", es: "No se proceso el pago. Debido a la alta demanda, ya no hay cupo disponible para este {registration}." },
    messageRegistrationAllWaitlisted: { en: "No payment was processed. Due to high demand there is no longer space available. The {waitlistedCountPhrase} added to the waitlist.", es: "No se proceso el pago. Debido a la alta demanda, ya no hay cupo disponible. Los {waitlistedCountPhrase} agregados a la lista de espera." },
    messageRegistrationSomeWaitlisted: { en: "This {registration} only had capacity for {registeredCountPhrase}. The first {firstWaitlistedPhrase} registered for {instanceName}. The remaining {remainingWaitlistedPhrase} added to the waitlist.", es: "Este {registration} solo tenia capacidad para {registeredCountPhrase}. Los primeros {firstWaitlistedPhrase} registrados para {instanceName}. Los {remainingWaitlistedPhrase} restantes fueron agregados a la lista de espera." },
    messageRegistrationCapacity: { en: "This {registration} only has capacity for {remainingCapacityPhrase}.", es: "Este {registration} solo tiene capacidad para {remainingCapacityPhrase}." },
    messageRegistrationWaitlistPrompt: { en: "This {registration} has reached its capacity. Complete the {registration} to be added to the waitlist.", es: "Este {registration} ha alcanzado su capacidad. Completa el {registration} para ser agregado a la lista de espera." },
    messageRegistrationWaitlistSplit: { en: "This {registration} only has capacity for {remainingCapacityPhrase}. The first {registeredPhrase} you add will be registered for {instanceName}. The remaining {waitlistedPhrase} will be added to the waitlist.", es: "Este {registration} solo tiene capacidad para {remainingCapacityPhrase}. Los primeros {registeredPhrase} que agregues seran registrados para {instanceName}. Los {waitlistedPhrase} restantes seran agregados a la lista de espera." },
    messageRegistrationDefaultSuccess: { en: "You have successfully completed this {registration}", es: "Has completado exitosamente este {registration}" },
    messagePaymentDeadlineRequired: { en: "A payment deadline date is required to create a payment plan.", es: "Se requiere una fecha limite de pago para crear un plan de pagos." },
    messagePaymentFrequencyIncompatible: { en: "The selected payment frequency is not compatible with the chosen start date. Please adjust the frequency or select a different start date to proceed.", es: "La frecuencia de pago seleccionada no es compatible con la fecha de inicio elegida. Ajusta la frecuencia o selecciona una fecha diferente para continuar." },
    messagePaymentPlanTotalDue: { en: "The total amount of {balanceDue} is payable by {deadline}.", es: "El monto total de {balanceDue} es pagadero antes del {deadline}." },
    messagePaymentScheduleDescription: { en: "Below are the dates and amounts that will be automatically charged to your chosen payment method.", es: "A continuacion se muestran las fechas y montos que se cargaran automaticamente a tu metodo de pago seleccionado." },
    messageRegistrantFullyRegistered: { en: "This {registrant} will be fully registered.", es: "Este {registrant} quedara completamente registrado." },
    messageRegistrantOnWaitlist: { en: "This {registrant} will be on the waiting list.", es: "Este {registrant} quedara en la lista de espera." },
    messageSelectValidPaymentOption: { en: "Please select a valid payment option.", es: "Selecciona un metodo de pago valido." },
    messageSessionTimeout: { en: "Due to a high-volume of expected interest, your {registration} session will expire after {minutes} of inactivity.", es: "Debido al alto volumen esperado, tu sesion de {registration} expirara despues de {minutes} de inactividad." },
    messageUnexpectedError: { en: "We could not complete the request. Please try again.", es: "No pudimos completar la solicitud. Intentalo nuevamente." },
    statusSuccess: { en: "Success", es: "Exito" },
    stepFinalize: { en: "Finalize", es: "Finalizar" },
    stepStart: { en: "Start", es: "Inicio" }
};

export type RegistrationEntryUiLanguage = "es" | "en";

const registrationEntryUiLanguageStorageKey = "rock.obsidian.uiLanguage";
let registrationEntryUiLanguageOverride: RegistrationEntryUiLanguage | null = null;

function normalizeRegistrationEntryUiLanguage(language: string | null | undefined): RegistrationEntryUiLanguage | null {
    if (!language) {
        return null;
    }

    const normalizedLanguage = language.toLowerCase();
    if (normalizedLanguage.startsWith("es")) {
        return "es";
    }

    if (normalizedLanguage.startsWith("en")) {
        return "en";
    }

    return null;
}

/** Gets the Registration Entry UI language. Defaults to Spanish (`es`). */
export function getRegistrationEntryUiLanguage(): RegistrationEntryUiLanguage {
    if (registrationEntryUiLanguageOverride) {
        return registrationEntryUiLanguageOverride;
    }

    if (typeof localStorage !== "undefined") {
        try {
            const storedLanguage = normalizeRegistrationEntryUiLanguage(localStorage.getItem(registrationEntryUiLanguageStorageKey));
            if (storedLanguage) {
                return storedLanguage;
            }
        }
        catch {
            // Ignore storage access errors.
        }
    }

    return "es";
}

/** Sets and persists the Registration Entry UI language. */
export function setRegistrationEntryUiLanguage(language: RegistrationEntryUiLanguage): void {
    registrationEntryUiLanguageOverride = language;

    if (typeof localStorage !== "undefined") {
        try {
            localStorage.setItem(registrationEntryUiLanguageStorageKey, language);
        }
        catch {
            // Ignore storage access errors.
        }
    }
}

/** Determines if the UI should prefer Spanish text. */
export function isSpanishUiLanguage(): boolean {
    return getRegistrationEntryUiLanguage() === "es";
}

/**
 * Localizes common registration terms when the browser is Spanish and the
 * registration template still contains English labels.
 */
export function localizeRegistrationEntryTerm(term: string): string {
    if (!isSpanishUiLanguage()) {
        return term;
    }

    const dictionary: Record<string, string> = {
        "person": "persona",
        "people": "personas",
        "registrant": "participante",
        "registrants": "participantes",
        "registration": "registro",
        "registrations": "registros",
        "fee": "cargo",
        "fees": "cargos",
        "discount code": "codigo de descuento",
        "discount codes": "codigos de descuento",
        "release": "exencion"
    };

    const termLowerCase = term.toLowerCase();
    return dictionary[termLowerCase] ?? term;
}

/**
 * Gets a translated registration entry string for the current browser language.
 * Supports `en` and `es`; defaults to `es`.
 */
export function getRegistrationEntryText(key: string, replacements?: Record<string, string | number>): string {
    const dictionaryEntry = registrationEntryText[key];
    let text = dictionaryEntry
        ? (isSpanishUiLanguage() ? dictionaryEntry.es : dictionaryEntry.en)
        : key;

    if (replacements) {
        for (const replacementKey of Object.keys(replacements)) {
            const token = `{${replacementKey}}`;
            text = text.split(token).join(`${replacements[replacementKey]}`);
        }
    }

    return text;
}

/** An injection key to provide the registration entry state. */
export const CurrentRegistrationEntryState: InjectionKey<RegistrationEntryState> = Symbol("registration-entry-state");

/** An injection key to provide the function that gets the args to persist the session. */
export const GetPersistSessionArgs: InjectionKey<() => RegistrationEntryArgsBag> = Symbol("get-persist-session-args");

/** An injection key to provide the function that persists the session. */
export const PersistSession: InjectionKey<(force?: boolean) => Promise<void>> = Symbol("persist-session");

/** An injection key to provide the cost summary for the entire registration. */
export const RegistrationCostSummary: InjectionKey<{
    readonlyRegistrationCostSummary: Ref<RegistrationCostSummaryInfo>;
    updateRegistrationCostSummary: (newValue: Partial<RegistrationCostSummaryInfo>) => void;
}> = Symbol("registration-cost-summary");

/**
 * An injection key to provide the data to configure a new payment plan.
 */
export const ConfigurePaymentPlan: InjectionKey<{
    wipPaymentPlanConfiguration: Ref<PaymentPlanConfiguration | null | undefined>;
    finalPaymentPlanConfiguration: Ref<PaymentPlanConfiguration | null | undefined>;
}> = Symbol("registration-configure-payment-plan");

/**
 * An injection key to provide the registration entry terms.
 */
export const RegistrationEntryTerms: InjectionKey<ComputedRef<RegistrationEntryTerminology>> = Symbol("registration-entry-terms");

/** An injection key to provide the original field values for each registrant. */
export const OriginalFormFieldValues: InjectionKey<Ref<Record<PersonGuid, Record<FormFieldGuid, FormFieldValue>>>> = Symbol("original-field-values");
