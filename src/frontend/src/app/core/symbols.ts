// Display vocabulary for the UI (glyphs, colours, Italian names of ids not already labelled by the API).

const TEXT = '︎'; // force text (not emoji) presentation

export const BODY_GLYPH: Record<string, string> = {
  sun: '☉', moon: '☽', mercury: '☿', venus: '♀', mars: '♂', jupiter: '♃', saturn: '♄',
  uranus: '♅', neptune: '♆', pluto: '♇', 'north-node': '☊', asc: 'AC', mc: 'MC',
};

export const SIGNS = [
  'aries', 'taurus', 'gemini', 'cancer', 'leo', 'virgo',
  'libra', 'scorpio', 'sagittarius', 'capricorn', 'aquarius', 'pisces',
] as const;

export const SIGN_GLYPH: Record<string, string> = {
  aries: '♈' + TEXT, taurus: '♉' + TEXT, gemini: '♊' + TEXT, cancer: '♋' + TEXT,
  leo: '♌' + TEXT, virgo: '♍' + TEXT, libra: '♎' + TEXT, scorpio: '♏' + TEXT,
  sagittarius: '♐' + TEXT, capricorn: '♑' + TEXT, aquarius: '♒' + TEXT, pisces: '♓' + TEXT,
};

export const SIGN_NAME: Record<string, string> = {
  aries: 'Ariete', taurus: 'Toro', gemini: 'Gemelli', cancer: 'Cancro', leo: 'Leone', virgo: 'Vergine',
  libra: 'Bilancia', scorpio: 'Scorpione', sagittarius: 'Sagittario', capricorn: 'Capricorno',
  aquarius: 'Acquario', pisces: 'Pesci',
};

/** Element colours, for the zodiac ring (fire, earth, air, water). */
export const SIGN_ELEMENT_COLOR = ['var(--fire)', 'var(--earth)', 'var(--air)', 'var(--water)'];

export const QUALITY_COLOR: Record<string, string> = {
  tension: 'var(--q-tension)',
  flow: 'var(--q-flow)',
  fusion: 'var(--q-fusion)',
  inward: 'var(--q-inward)',
  agitation: 'var(--q-agitation)',
  depletion: 'var(--q-depletion)',
  pressure: 'var(--q-pressure)',
  sensitivity: 'var(--q-sensitivity)',
  neutral: 'var(--q-neutral)',
};

export const QUALITY_NAME: Record<string, string> = {
  tension: 'tensione', flow: 'flusso', fusion: 'fusione', inward: 'introversione',
  agitation: 'agitazione', depletion: 'svuotamento', pressure: 'pressione',
  sensitivity: 'sensibilità', neutral: 'neutra',
};

/** Same colours as the --q-* variables in styles.scss, for canvas/WebGL drawing. */
export const QUALITY_HEX: Record<string, string> = {
  tension: '#e0565b',
  flow: '#3fb8a5',
  fusion: '#e2b84a',
  inward: '#a081e6',
  agitation: '#f08a3c',
  depletion: '#5182e0',
  pressure: '#9aa3b5',
  sensitivity: '#e07bb5',
  neutral: '#7d7891',
};

export const qualityHex = (q: string | null | undefined) => QUALITY_HEX[q ?? 'neutral'] ?? QUALITY_HEX['neutral'];

export const qualityColor = (q: string | null | undefined) => QUALITY_COLOR[q ?? 'neutral'] ?? 'var(--q-neutral)';

export const METRIC_NAME: Record<string, string> = {
  energy: 'Energia', mood: 'Umore', sleep: 'Sonno', focus: 'Lucidità',
  stress: 'Stress', gut: 'Intestino', head: 'Testa', body: 'Corpo',
};

export const METRIC_HINT: Record<string, string> = {
  energy: '1 a terra · 5 carico',
  mood: '1 cupo · 5 luminoso',
  sleep: '1 pessimo · 5 profondo',
  focus: '1 nebbia · 5 limpido',
  stress: '1 calmo · 5 sotto pressione',
  gut: '1 in subbuglio · 5 tranquillo',
  head: '1 dolore/pesantezza · 5 leggera',
  body: '1 dolori/tensioni · 5 sciolto',
};

export const KIND_GROUP: Record<string, string> = {
  'retrograde-count': 'Cielo',
  retrograde: 'Cielo',
  station: 'Cielo',
  'slow-motion': 'Cielo',
  'transit-aspect': 'Transiti sul tema natale',
  'sky-aspect': 'Aspetti nel cielo',
  eclipse: 'Cielo',
  'lunar-phase': 'Luna e luce',
  'lunar-distance': 'Luna e luce',
  photoperiod: 'Luna e luce',
  'photoperiod-change': 'Luna e luce',
  geomagnetic: 'Sole e magnetosfera',
  pressure: 'Meteo',
  'pressure-change': 'Meteo',
  'temperature-swing': 'Meteo',
  humidity: 'Meteo',
  dryness: 'Meteo',
  wind: 'Meteo',
  temperature: 'Meteo',
  schumann: 'Sole e magnetosfera',
  'solar-term': 'Calendario cinese',
  'organ-clock': 'Calendario cinese',
  'hd-gate': 'Human Design',
  decan: 'Cielo',
  'fixed-star': 'Cielo',
  'planetary-hour': 'Tempi tradizionali',
  'lunar-mansion': 'Luna e luce',
  nakshatra: 'Luna e luce',
  ganzhi: 'Calendario cinese',
  septennial: 'Tempi della vita',
  profection: 'Tempi della vita',
  dasha: 'Tempi della vita',
  dignity: 'Cielo',
  'solar-hour': 'Tempi tradizionali',
  'sidereal-placement': 'Luna e luce',
};

/** Display order of the signal groups (Adesso, Timeline). */
export const GROUP_ORDER = [
  'Cielo', 'Transiti sul tema natale', 'Aspetti nel cielo', 'Luna e luce', 'Sole e magnetosfera', 'Meteo',
  'Calendario cinese', 'Tempi tradizionali', 'Tempi della vita', 'Human Design',
];
