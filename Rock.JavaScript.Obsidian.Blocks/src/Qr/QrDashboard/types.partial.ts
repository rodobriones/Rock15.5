// Copyright by the Spark Development Network; Licensed under the Rock Community License
//
// Tipos espejo de Rock.ViewModels/Blocks/Qr/QrDashboard/QrDashboardBags.cs.

/** Un indicador con su comparación contra el período anterior. */
export type Kpi = {
    label: string;
    value: number;

    /** Null cuando no hubo período anterior que medir: un 0 diría que lo hubo y dio cero. */
    previousValue: number | null;

    /** Null cuando el período anterior fue cero: un "+∞%" no informa nada. */
    changePercent: number | null;

    /** Falso para dormidos y nunca escaneados, donde subir es malo. */
    higherIsBetter: boolean;

    note?: string | null;
};

import { SectionMeta } from "../QrShared/types.partial";

export type Slice = {
    name: string;
    count: number;
};

export type DashboardData = {
    rangeStart: string;
    rangeEnd: string;
    kpis: Kpi[];

    totalCodes: number;
    dynamicCodes: number;
    staticCodes: number;
    retiredCodes: number;
    byCategory: Slice[];
    byCreator: Slice[];
    codesByMonth: Slice[];

    codesWithScans: number;
    codesNeverScanned: number;
    staleCodes: number;

    /** Dormidos que SÍ se escanearon alguna vez. Usar este, no restar buckets. */
    staleCodesEverScanned: number;

    /** Subconjunto de codesNeverScanned demasiado nuevo para juzgar. */
    codesNeverScannedTooNew: number;

    byCategoryMeta: SectionMeta;
    byCreatorMeta: SectionMeta;
    byCampaignMeta: SectionMeta;

    byWeekday: Slice[];
    byHour: Slice[];
    byCampaign: Slice[];

    /** Advertencia cuando hay short links fuera del catálogo. */
    scopeNote?: string | null;
};

export type DashboardOptions = {
    // Este panel no lista códigos uno por uno — sus barras son categorías, creadores, meses y
    // campañas —, así que no hay fila que enlazar a un detalle. El campo detailPageUrl que
    // mandaba el servidor estaba declarado y nunca usado: se quita para que nadie lo lea como
    // una funcionalidad pendiente. Los enlaces por código viven en la pantalla de Uso.
    metricsPageUrl: string | null;
    catalogPageUrl: string | null;
    staleDays: number;
};
