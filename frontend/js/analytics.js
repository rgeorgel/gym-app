/**
 * Google Analytics 4 event tracking helpers.
 * gtag is loaded globally via the inline script in each HTML page.
 */

export function trackEvent(eventName, params = {}) {
  if (typeof gtag === 'undefined') return;
  gtag('event', eventName, params);
}

export function trackPageView(pageName) {
  if (typeof gtag === 'undefined') return;
  gtag('event', 'page_view', {
    page_title: pageName,
    page_location: window.location.href,
    page_path: window.location.pathname + window.location.search + window.location.hash,
  });
}
