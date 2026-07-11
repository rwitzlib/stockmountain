export function formatCurrency(value: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  }).format(value);
}

/** "+$983.64" / "-$49.57" — explicit sign for P&L values. */
export function formatSignedCurrency(value: number): string {
  return `${value > 0 ? '+' : ''}${formatCurrency(value)}`;
}

/** "+9.84%" / "-8.35%" */
export function formatSignedPercent(value: number, digits = 2): string {
  return `${value > 0 ? '+' : ''}${value.toFixed(digits)}%`;
}

/** Compact dollars for chart axes: "$10.5k", "$250" */
export function formatAxisCurrency(value: number): string {
  const abs = Math.abs(value);
  const sign = value < 0 ? '-' : '';
  if (abs >= 1000) {
    const k = abs / 1000;
    return `${sign}$${k >= 100 ? k.toFixed(0) : k % 1 ? k.toFixed(1) : k.toFixed(0)}k`;
  }
  return `${sign}$${abs % 1 ? abs.toFixed(2) : abs.toFixed(0)}`;
}

/** "Jun 5" style short date from a YYYY-MM-DD or ISO string, timezone-safe. */
export function formatShortDate(date: string): string {
  const day = date.slice(0, 10);
  return new Date(`${day}T12:00:00`).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
}