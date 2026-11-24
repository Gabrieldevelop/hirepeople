/** Fecha de hoy en hora local (YYYY-MM-DD). toISOString() usa UTC y puede dar el día siguiente por la noche. */
export const hoyLocal = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};

/** "07:57:00" -> "07:57" */
export const hora = (h?: string | null) => (h ? h.slice(0, 5) : '—');

export const dinero = (n: number) => (n ?? 0).toLocaleString('es-DO', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export const fecha = (f?: string | null) => (f ? new Date(f + (f.length === 10 ? 'T00:00:00' : '')).toLocaleDateString('es-DO') : '—');

export const mensajeError = (e: any, def: string) => e?.response?.data?.mensaje ?? def;

export const MESES = ['', 'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];
