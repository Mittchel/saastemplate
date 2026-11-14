import Link from "next/link";

export const metadata = { title: "Terms of Service" };

/**
 * Placeholder.
 *
 * Deliberately not filled with plausible-looking legal text: invented terms
 * that read as real are worse than an obvious blank, because they are easy to
 * ship by accident and they are not advice anyone should rely on.
 */
export default function TermsPage() {
  return (
    <main className="mx-auto max-w-2xl px-6 py-16">
      <h1 className="text-2xl font-semibold">Terms of Service</h1>
      <p className="text-muted-foreground mt-4 text-sm">
        This page is a placeholder in the template. Replace it with terms written
        for your product, by someone qualified to write them.
      </p>
      <p className="text-muted-foreground mt-4 text-sm">
        The login and sign-up forms link here, so a product that ships without
        replacing this page is linking its users to nothing.
      </p>
      <Link href="/login" className="mt-8 inline-block text-sm underline underline-offset-4">
        Back to sign in
      </Link>
    </main>
  );
}
