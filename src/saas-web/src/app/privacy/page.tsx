import Link from "next/link";

export const metadata = { title: "Privacy Policy" };

/**
 * Placeholder.
 *
 * Deliberately not filled with plausible-looking legal text: invented terms
 * that read as real are worse than an obvious blank, because they are easy to
 * ship by accident and they are not advice anyone should rely on.
 */
export default function PrivacyPage() {
  return (
    <main className="mx-auto max-w-2xl px-6 py-16">
      <h1 className="text-2xl font-semibold">Privacy Policy</h1>
      <p className="text-muted-foreground mt-4 text-sm">
        This page is a placeholder in the template. Replace it with a privacy policy written
        for your product, by someone qualified to write them.
      </p>
      <p className="text-muted-foreground mt-4 text-sm">
        The login and sign-up forms link here. A real policy also has to describe
        what this stack actually stores: account details, workspace membership,
        and every prompt and response passed to the model provider.
      </p>
      <Link href="/login" className="mt-8 inline-block text-sm underline underline-offset-4">
        Back to sign in
      </Link>
    </main>
  );
}
