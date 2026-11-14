"use client";

import { ThemeProvider as NextThemesProvider } from "next-themes";

/**
 * Applies the light or dark theme.
 *
 * shadcn's dark mode is class-based (`@custom-variant dark (&:is(.dark *))` in
 * globals.css), so something has to put `.dark` on the html element. Without
 * this the app renders light regardless of the operating system setting.
 *
 * `defaultTheme="system"` follows the OS and `enableSystem` keeps it in sync
 * when the user changes it. `disableTransitionOnChange` stops every colour on
 * the page animating at once during a switch.
 */
export function ThemeProvider({ children }: { children: React.ReactNode }) {
  return (
    <NextThemesProvider
      attribute="class"
      defaultTheme="system"
      enableSystem
      disableTransitionOnChange
    >
      {children}
    </NextThemesProvider>
  );
}
