// Icon registry: the backend's icon ids (schools, influences) → Material Symbols names (rounded set).
// The font is loaded in src/index.html with `icon_names=` so only these glyphs are downloaded:
// a name added here (or used directly in a template) must be added there too, keeping the list sorted.
// Browse names at https://fonts.google.com/icons.

export const ICONS: Record<string, string> = {
  // schools: how a tradition reads the facts onto the body
  'school-melothesia': 'accessibility_new', // the zodiac man
  'school-biometeorology': 'thermostat', // the body and the weather
  'school-tcm': 'spa', // Chinese medicine: balance, five phases
  'school-human-design': 'hub', // the bodygraph: centers joined by channels
  'school-tantric-yoga': 'self_improvement', // the meditating yogi: chakras and nadis

  // influences: where the facts come from
  ephemeris: 'orbit', // planets on their orbits
  gfz: 'flare', // the Sun's storms on the Earth's magnetic field
  'open-meteo': 'partly_cloudy_day', // local weather
  schumann: 'graphic_eq', // a resonance between ground and ionosphere
  'tcm-calendar': 'calendar_month', // solar terms and the organ clock
  'human-design': 'hexagon', // the 64 hexagrams on the zodiac

  // app
  now: 'nightlight',
  timeline: 'timeline',
  journal: 'edit_note',
  correlations: 'insights',
  catalog: 'menu_book',
  profile: 'account_circle',
  facts: 'sensors',
  consequences: 'body_system',
  place: 'location_on',
  close: 'close',
  home_pin: 'home_pin',
};

export const FALLBACK_ICON = 'auto_awesome';

export const iconName = (id: string) => ICONS[id] ?? FALLBACK_ICON;
