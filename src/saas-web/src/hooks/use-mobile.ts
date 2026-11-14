import * as React from "react";

const MOBILE_BREAKPOINT = 768;

const QUERY = `(max-width: ${MOBILE_BREAKPOINT - 1}px)`;

/**
 * Whether the viewport is phone-sized.
 *
 * Rewritten from the version shadcn generates, which set state inside the
 * effect to seed the initial value. React's `set-state-in-effect` lint rule
 * rejects that -- it causes a second render pass on every mount.
 * `useSyncExternalStore` is the intended API for subscribing to something
 * outside React, and it reads the current value during render instead.
 */
function subscribe(onChange: () => void): () => void {
  const query = window.matchMedia(QUERY);

  query.addEventListener("change", onChange);

  return () => query.removeEventListener("change", onChange);
}

function getSnapshot(): boolean {
  return window.matchMedia(QUERY).matches;
}

// The server has no viewport. Reporting "not mobile" matches the sidebar's
// desktop-first markup, so hydration does not have to correct itself.
function getServerSnapshot(): boolean {
  return false;
}

export function useIsMobile(): boolean {
  return React.useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);
}
