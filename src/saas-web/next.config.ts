import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Emits a self-contained server bundle with only the node_modules actually
  // reached, which is what keeps the container image small.
  output: "standalone",

  // `cacheComponents` (the create-next-app default) requires every route that
  // touches per-request data to declare a prerenderable shell. In this app
  // there is no such shell: the layout itself reads the session and calls
  // GET /me, and every page below it is scoped to one user and one tenant, so
  // nothing is cacheable across requests. Leaving it on would mean scattering
  // Suspense boundaries and `instant = false` opt-outs over every route to buy
  // a static shell that does not exist. Turn it back on for a product that
  // grows genuinely public, cacheable pages.
  // partialPrefetching depends on cacheComponents, so it goes with it.
  cacheComponents: false,

  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

export default nextConfig;
