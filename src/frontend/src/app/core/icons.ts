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
  'school-traditional-astrology': 'brightness_5', // the twelve houses around the chart
  'school-jyotish': 'temple_hindu', // Indian astrology
  'school-ayurveda': 'eco', // the three doshas
  'school-galenic': 'water_drop', // the four humours
  'school-kabbalah': 'account_tree', // the Tree of Life
  'school-steiner': 'stacks', // the four members of the human being
  'school-chronobiology': 'routine', // the circadian clock

  // influences: where the facts come from
  ephemeris: 'orbit', // planets on their orbits
  gfz: 'flare', // the Sun's storms on the Earth's magnetic field
  'open-meteo': 'partly_cloudy_day', // local weather
  schumann: 'graphic_eq', // a resonance between ground and ionosphere
  'tcm-calendar': 'calendar_month', // solar terms and the organ clock
  'human-design': 'hexagon', // the 64 hexagrams on the zodiac
  'planetary-hours': 'schedule', // the hours of the planets
  'lunar-stations': 'bedtime', // the Moon's mansions and nakshatras
  'life-times': 'hourglass', // septennials, profections, dashas
  'noaa-swpc': 'satellite_alt', // the solar wind measured in space
  'air-quality': 'air', // air, pollen, UV
  decumbiture: 'healing', // the course of a malaise

  // app
  now: 'nightlight',
  timeline: 'timeline',
  journal: 'edit_note',
  correlations: 'insights',
  catalog: 'menu_book',
  profile: 'account_circle',
  facts: 'sensors',
  consequences: 'body_system',
  back: 'arrow_back',
  flow: 'hub',
  tree: 'account_tree',
  arrow: 'arrow_forward',
  expand: 'open_in_full',
  collapse: 'close_fullscreen',
  eye: 'visibility',
  'eye-off': 'visibility_off',
  hint: 'lightbulb',
  layers: 'layers',
  rule: 'rule',
  sky: 'public',
  schedule: 'schedule',
  place: 'location_on',
  close: 'close',
  home_pin: 'home_pin',
};

export const FALLBACK_ICON = 'auto_awesome';

export const iconName = (id: string) => ICONS[id] ?? FALLBACK_ICON;
