// Internationalisation — central translation module

let locale = 'pt-BR';
const _translations = {};

export function setLocale(lang) {
  locale = lang || 'pt-BR';
}

export function getLocale() {
  return locale;
}

export function loadTranslations(lang, dict) {
  _translations[lang] = dict;
}

/**
 * Translate a key, with optional template params.
 * t('btn.save')
 * t('packages.usedOf', { used: 3, total: 10 })
 */
export function t(key, params) {
  let str = _translations[locale]?.[key] ?? _translations['pt-BR']?.[key] ?? key;
  if (params) {
    str = str.replace(/\{(\w+)\}/g, (_, k) => params[k] ?? '');
  }
  return str;
}

/** Locale-aware weekday short names ['Dom','Seg',...] / ['Sun','Mon',...] */
export function getWeekdays() {
  return locale === 'en-US'
    ? ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']
    : ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];
}

/** Locale-aware weekday full names */
export function getWeekdaysFull() {
  return locale === 'en-US'
    ? ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']
    : ['Domingo', 'Segunda', 'Terça', 'Quarta', 'Quinta', 'Sexta', 'Sábado'];
}

/** Locale-aware month short names */
export function getMonthNames() {
  return locale === 'en-US'
    ? ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
    : ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez'];
}
