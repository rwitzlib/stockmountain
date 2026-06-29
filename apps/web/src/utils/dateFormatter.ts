export function formatDateTime(date: Date | string | number): string {
  const d = new Date(date);
  // Convert to NY timezone
  return d.toLocaleString('en-US', {
    timeZone: 'America/New_York',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: true
  });
}

export function formatDateTimeNoMinutes(date: Date | string | number): string {
  const d = new Date(date);
  // Convert to NY timezone
  return d.toLocaleString('en-US', {
    timeZone: 'America/New_York',
    month: 'short',
    day: 'numeric',
    year: 'numeric'
  });
}

export function formatDateNoTimezone(date: Date | string | number): string {
  if (typeof date === 'string') {
    // Handle different string formats
    if (date.match(/^\d{4}-\d{2}-\d{2}$/)) {
      // Simple YYYY-MM-DD format
      const [year, month, day] = date.split('-').map(Number);
      
      const monthNames = [
        'January', 'February', 'March', 'April', 'May', 'June',
        'July', 'August', 'September', 'October', 'November', 'December'
      ];
      
      return `${monthNames[month - 1]} ${day}, ${year}`;
    } else if (date.match(/^\d{4}-\d{2}-\d{2}T/)) {
      // ISO string format like "2025-05-01T00:00:00.000Z"
      const datePart = date.split('T')[0]; // Get just the date part
      const [year, month, day] = datePart.split('-').map(Number);
      
      const monthNames = [
        'January', 'February', 'March', 'April', 'May', 'June',
        'July', 'August', 'September', 'October', 'November', 'December'
      ];
      
      return `${monthNames[month - 1]} ${day}, ${year}`;
    }
  }
  
  // For other formats, use Date object
  const d = new Date(date);
  return d.toLocaleDateString('en-US', {
    month: 'long',
    day: 'numeric',
    year: 'numeric'
  });
}

export function formatDate(dateString: string): string {
  return new Date(dateString).toLocaleDateString('en-US', {
    timeZone: 'America/New_York',
    month: 'short',
    day: 'numeric'
  });
}

export function formatDateTimeWithHours(dateString: string): string {
  return formatDateTime(dateString);
}