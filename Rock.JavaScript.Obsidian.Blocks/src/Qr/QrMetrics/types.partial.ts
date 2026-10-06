// Copyright by the Spark Development Network; Licensed under the Rock Community License
//
// Tipos espejo de Rock.ViewModels/Blocks/Qr/QrMetrics/QrMetricsBags.cs.

import { SectionMeta } from "../QrShared/types.partial";

export type TimelinePoint = {
    date: string;
    count: number;
};

export type NamedCount = {
    name: string;
    count: number;
};

export type TopCode = {
    idKey?: string | null;
    name?: string | null;
    categoryName?: string | null;
    count: number;
    lastScanDateTime?: string | null;

    /** Lleva más del plazo configurado sin un escaneo. */
    isStale: boolean;
};

/**
 * Etiqueta del bucket que agrupa los escaneos cuya IP no se resolvió porque quedó fuera de la
 * muestra (se consultan hasta 500 IP distintas por carga). Viene con countryCode en null.
 * NO es lo mismo que "Desconocido": ahí la consulta se hizo y no ubicó nada.
 */
export const UNRESOLVED_COUNTRY_NAME = "No resuelto (muestra parcial)";

/** Procedencia agregada. Nunca trae la IP. */
export type GeoCount = {
    countryCode?: string | null;
    countryName?: string | null;
    regionName?: string | null;
    city?: string | null;
    count: number;
};

export type MetricsData = {
    rangeStart: string;
    rangeEnd: string;
    totalScans: number;

    /** Atribuibles a una persona; depende de EnableVisitorTracking en el sitio. */
    identifiedScans: number;

    activeCodes: number;

    /**
     * Cuántos códigos del catálogo son dinámicos. Solo ellos dejan escaneos, así que con 0 el
     * panel no puede decir "no hay escaneos en este rango": no los hay en ninguno.
     */
    dynamicCodeCount: number;

    /** Activos nunca escaneados pero demasiado nuevos para juzgar. Fuera de staleCodes. */
    codesNeverScannedTooNew: number;

    topCodesMeta: SectionMeta;
    staleListMeta: SectionMeta;
    byOperatingSystemMeta: SectionMeta;
    byCountryMeta: SectionMeta;

    /** Ojo: totalValue son los escaneos de las ciudades del país top, no los del panel. */
    byCityMeta: SectionMeta;

    staleCodes: number;
    distinctCountries: number;
    timeline: TimelinePoint[];

    /**
     * Arriba de 400 días la serie se agrupa por SEMANA y cada punto es el primer día de la suya.
     * Leer un pico semanal como si fuera diario es errar por un orden de magnitud.
     */
    timelineIsWeekly: boolean;
    topCodes: TopCode[];
    staleList: TopCode[];
    byClientType: NamedCount[];
    byOperatingSystem: NamedCount[];
    byCountry: GeoCount[];
    byCity: GeoCount[];

    /** Falso si la base de geolocalización no está disponible. */
    geoAvailable: boolean;

    /** Aviso sobre la calidad del dato, o null. */
    qualityNote?: string | null;
};

export type MetricsOptions = {
    /** Plantilla de URL al detalle, con ((Key)) donde va el IdKey. */
    detailPageUrl: string | null;
    catalogPageUrl: string | null;
    dashboardPageUrl: string | null;

    staleDays: number;
};
