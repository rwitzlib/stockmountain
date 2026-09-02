/** Money and date formatting shared by the billing page and its dialogs. */

export function formatMoney(cents: number, currency: string | undefined): string {
  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: (currency ?? 'usd').toUpperCase(),
  }).format(cents / 100);
}

export function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  });
}
