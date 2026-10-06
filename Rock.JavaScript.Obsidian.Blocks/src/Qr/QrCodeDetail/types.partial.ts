// Copyright by the Spark Development Network; Licensed under the Rock Community License
//
// Tipos espejo de los bags C# de Rock.ViewModels/Blocks/Qr/QrCodeDetail/.
// Se declaran a mano, igual que en los bloques de Eventos: los módulos propios de Vida Real no
// dependen del code-gen de ViewModels del core.

import { ListItemBag } from "@Obsidian/ViewModels/Utility/listItemBag";

/** Espejo de Rock.Enums.Qr.QrCodeType. */
export const enum QrType {
    /** Redirige por un enlace corto: destino editable y métricas. */
    Dinamico = 0,

    /** Lleva el contenido dentro del código: inmutable y sin métricas. */
    Estatico = 1
}

/** Espejo de Rock.Enums.Qr.QrStaticContentType. */
export const enum StaticType {
    Url = 0,
    Texto = 1,
    VCard = 2,
    Wifi = 3,
    Email = 4,
    Telefono = 5
}

/** Colores y logo. El contraste y el nivel ECC los calcula el servidor. */
export type DesignBag = {
    foregroundColor: string;
    backgroundColor: string;
    logoSizePercent: number;
    contrastRatio: number;
    validationMessage: string | null;
    eccLevel: string | null;
};

export type CodeBag = {
    idKey?: string | null;
    name?: string | null;
    description?: string | null;
    category?: ListItemBag | null;
    qrType: number;

    /** Falso solo mientras el código es nuevo: el tipo se congela al guardar. */
    isTypeLocked: boolean;

    token?: string | null;
    shortLinkUrl?: string | null;

    /**
     * Destino BASE del enlace corto, editable. Si el código tiene destinos programados por fecha,
     * este NO es a dónde lleva hoy: para eso está effectiveDestination.
     */
    destination?: string | null;

    /** A dónde redirige hoy, resuelto por el servidor. Solo lectura. */
    effectiveDestination?: string | null;

    /** El enlace corto tiene destinos programados por fecha, editables desde la pantalla nativa. */
    hasSchedules: boolean;

    staticContentType?: number | null;

    /** JSON para wifi y vCard; el valor plano para los demás tipos. */
    staticContent?: string | null;

    design: DesignBag;
    logoBinaryFile?: ListItemBag | null;
    previewSvg?: string | null;
    isActive: boolean;
    retiredDateTime?: string | null;
    scanCount: number;
    lastScanDateTime?: string | null;
};

export type OptionsBag = {
    categories: ListItemBag[];
    staticContentTypes: ListItemBag[];

    /**
     * El bloque se inicializa con VIEW (para que descargar no exija editar), así que acá puede
     * llegar alguien sin permiso de edición. Save, Retire, Reactivate y Preview lo siguen
     * exigiendo en el servidor: esto solo evita que el formulario prometa lo que no puede cumplir.
     */
    canEdit: boolean;

    /** Falso si no hay sitio, o si el sitio no tiene EnabledForShortening: sin eso los códigos dinámicos darían 404. */
    hasShortDomain: boolean;

    shortDomain: string | null;
    shortDomainMessage: string | null;
    maxLogoSizePercent: number;
    minContrastRatio: number;
    minPrintSideCm: number;

    /** URL del catálogo, para el enlace de volver. Null si el bloque no la tiene configurada. */
    listPageUrl: string | null;
};
