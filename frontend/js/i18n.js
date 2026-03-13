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

/** Locale-aware weekday short names */
export function getWeekdays() {
  if (locale === 'en-US') return ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
  if (locale === 'es-ES') return ['Dom', 'Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb'];
  return ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];
}

/** Locale-aware weekday full names */
export function getWeekdaysFull() {
  if (locale === 'en-US') return ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
  if (locale === 'es-ES') return ['Domingo', 'Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado'];
  return ['Domingo', 'Segunda', 'Terça', 'Quarta', 'Quinta', 'Sexta', 'Sábado'];
}

/** Locale-aware month short names */
export function getMonthNames() {
  if (locale === 'en-US') return ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  if (locale === 'es-ES') return ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];
  return ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez'];
}
