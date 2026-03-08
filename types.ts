export interface Manifest {
  id: string;
  name: string;
  version: string;
  description?: string;
  resources: (string | { name: string; types: string[]; idPrefixes?: string[] })[];
  types: string[];
  catalogs: CatalogDefinition[];
  background?: string;
  logo?: string;
  idPrefixes?: string[];
}

export interface CatalogDefinition {
  type: string;
  id: string;
  name?: string;
  extra?: { name: string; isRequired?: boolean; options?: string[] }[];
}

export interface MetaPreview {
  id: string;
  type: string;
  name: string;
  poster?: string;
  background?: string;
  logo?: string;
  description?: string;
  releaseInfo?: string;
  imdbRating?: string;
  genres?: string[];
}

export interface MetaDetail extends MetaPreview {
  videos?: Video[];
  runtime?: string;
  cast?: string[];
  director?: string[];
  year?: number;
  trailers?: { source: string; type: string }[];
}

export interface Video {
  id: string;
  title: string;
  released: string;
  thumbnail?: string;
  episode?: number;
  season?: number;
  overview?: string;
}

export interface Stream {
  name: string;
  title?: string;
  description?: string;
  url?: string;
  infoHash?: string;
  fileIdx?: number;
  externalUrl?: string;
  sources?: string[];
  behaviorHints?: {
    bingeGroup?: string;
    notWebReady?: boolean;
    countryWhitelist?: string[];
  };
  /** Added by addonService – which addon provided this stream */
  addonName?: string;
  addonId?: string;
  /** Custom HTTP headers required to play the stream (e.g. Referer) */
  headers?: Record<string, string>;
  /** Stream type hint: 'hls', 'dash', or 'direct' */
  type?: "hls" | "dash" | "direct";
}

export interface Addon {
  baseUrl: string;
  manifest: Manifest;
  enabled: boolean;
}
