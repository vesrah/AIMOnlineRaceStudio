/** Extract a display message from an unknown caught value; use fallback if not an Error. */
export function getErrorMessage(e: unknown, fallback: string): string {
  return e instanceof Error ? e.message : fallback;
}
