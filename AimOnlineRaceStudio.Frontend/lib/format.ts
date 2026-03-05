/** Format seconds as minutes:seconds:milliseconds (3 digits), e.g. 1:23:456 */
export function formatLapTime(seconds: number): string {
  const totalMs = Math.round(seconds * 1000);
  const mins = Math.floor(totalMs / 60000);
  const secs = Math.floor((totalMs % 60000) / 1000);
  const ms = totalMs % 1000;
  return `${mins}:${secs.toString().padStart(2, '0')}:${ms.toString().padStart(3, '0')}`;
}
