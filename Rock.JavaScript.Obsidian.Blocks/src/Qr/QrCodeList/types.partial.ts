// Copyright by the Spark Development Network; Licensed under the Rock Community License
//
// Tipos espejo de Rock.ViewModels/Blocks/Qr/QrCodeList/QrCodeListBags.cs.

import { ListItemBag } from "@Obsidian/ViewModels/Utility/listItemBag";

/** Una fila del catálogo. */
export type RowBag = {
    idKey?: string | null;
    name?: string | null;
    description?: string | null;
    categoryName?: string | null;

    /** Identidad real de la categoría. El nombre se repite y se renombra; el Guid no. */
    categoryGuid?: string | null;

    /** 0 dinámico, 1 estático. Espejo de QrType. */
    qrType: number;

    shortLinkUrl?: string | null;

    /** Leído del enlace corto, no copiado. Null en estáticos: su contenido no se lista. */
    destination?: string | null;

    createdByName?: string | null;
    createdDateTime?: string | null;

    /** Siempre 0 en estáticos: no hay redirección que registrar. */
    scanCount: number;

    lastScanDateTime?: string | null;
    isActive: boolean;
};

export type ListOptionsBag = {
    categories: ListItemBag[];
    canEdit: boolean;

    /** Plantilla de URL al detalle, con ((Key)) donde va el IdKey. */
    detailPageUrl: string | null;

    /** Reportería y uso. El menú de Rock solo muestra dos niveles: sin esto no se llega. */
    dashboardPageUrl: string | null;
    metricsPageUrl: string | null;

    /** Días sin escaneo tras los cuales un código dinámico se marca como dormido. */
    staleDays: number;
};
