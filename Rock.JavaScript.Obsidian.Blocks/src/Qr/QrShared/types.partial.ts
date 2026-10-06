// Copyright by the Spark Development Network; Licensed under the Rock Community License
//
// Espejo de Rock.ViewModels.Blocks.Qr.QrSectionMetaBag.

/**
 * Cuánto de una sección se está mostrando. Existe porque todas las listas del módulo van
 * recortadas (Take(n)) y el cliente escala la barra al máximo de la lista, no al total: sin
 * esto, la primera barra siempre llega al 100 % y sugiere "todo" cuando puede ser el 4 %.
 *
 * `value` es escaneos en las secciones de uso y CÓDIGOS en las de composición.
 */
export type SectionMeta = {
    shownCount: number;
    totalCount: number;
    shownValue: number;
    totalValue: number;
};

/** Hay recorte cuando se muestra menos de lo que hay. */
export function isTruncated(meta: SectionMeta | null | undefined): boolean {
    return !!meta && meta.shownCount < meta.totalCount;
}

/** Línea al pie de una lista recortada. Devuelve "" cuando no hay nada que aclarar. */
export function truncationNote(meta: SectionMeta | null | undefined, unidad: string): string {
    if (!isTruncated(meta) || !meta) { return ""; }

    const n = (v: number): string => v.toLocaleString("es-GT");

    return `Mostrando ${n(meta.shownCount)} de ${n(meta.totalCount)}`
        + ` · ${n(meta.shownValue)} de ${n(meta.totalValue)} ${unidad}`;
}
