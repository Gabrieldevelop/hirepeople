// Un método por endpoint, con las rutas exactas de la "Referencia de API" del PDF.
import { api } from './client';
const d = <T = any>(p: Promise<{ data: T }>) => p.then((r) => r.data);

export const authApi = {
  login: (correo: string, password: string) => d(api.post('/api/auth/login', { correo, password })),
  logout: (refreshToken: string) => d(api.post('/api/auth/logout', { refreshToken })),
  me: () => d(api.get('/api/auth/me')),
};

export const empleadosApi = {
  list: (params: { departamento?: string; estado?: string; buscar?: string; pagina?: number }) =>
    d(api.get('/api/empleados', { params })),
  get: (id: string | number) => d(api.get(`/api/empleados/${id}`)),
  create: (body: any) => d(api.post('/api/empleados', body)),
  update: (id: string | number, body: any) => d(api.put(`/api/empleados/${id}`, body)),
  setEstado: (id: string | number, estado: string) => d(api.patch(`/api/empleados/${id}/estado`, { estado })),
  // Acceso auditado en el backend: solo se llama cuando el usuario pulsa "Ver".
  datosSensibles: (id: string | number) => d(api.get(`/api/empleados/${id}/datos-sensibles`)),
  contratos: (id: string | number) => d(api.get(`/api/empleados/${id}/contratos`)),
  saldoVacaciones: (id: string | number) => d(api.get(`/api/empleados/${id}/saldo-vacaciones`)),
  evaluaciones: (id: string | number) => d(api.get(`/api/empleados/${id}/evaluaciones`)),
};

export const catalogosApi = {
  departamentos: () => d(api.get('/api/departamentos')),
  cargos: () => d(api.get('/api/cargos')),
  tiposAusencia: () => d(api.get('/api/tipos-ausencia')),
};

export const asistenciaApi = {
  porFecha: (fecha: string, empleadoId?: number) => d(api.get('/api/asistencia', { params: { fecha, empleadoId } })),
  marcar: () => d(api.post('/api/asistencia/marcas')),
  corregir: (id: number, body: any) => d(api.put(`/api/asistencia/${id}`, body)),
  resumen: (desde: string, hasta: string) => d(api.get('/api/asistencia/resumen', { params: { desde, hasta } })),
};

export const ausenciasApi = {
  list: (params: { estado?: string; empleadoId?: number }) => d(api.get('/api/ausencias', { params })),
  create: (body: any) => d(api.post('/api/ausencias', body)),
  // La API espera { decision: 'Aprobar' | 'Rechazar', comentario }.
  decidir: (id: number, aprobada: boolean, comentario: string) =>
    d(api.patch(`/api/ausencias/${id}/aprobacion`, { decision: aprobada ? 'Aprobar' : 'Rechazar', comentario })),
  cancelar: (id: number) => d(api.delete(`/api/ausencias/${id}`)),
};

export const nominaApi = {
  periodos: () => d(api.get('/api/nomina/periodos')),
  crearPeriodo: (body: any) => d(api.post('/api/nomina/periodos', body)),
  calcular: (id: number) => d(api.post(`/api/nomina/periodos/${id}/calcular`)),
  cerrar: (id: number) => d(api.post(`/api/nomina/periodos/${id}/cerrar`)),
  recibos: (id: number) => d(api.get(`/api/nomina/periodos/${id}/recibos`)),
  misRecibos: () => d(api.get('/api/nomina/mis-recibos')),
  recibo: (id: number) => d(api.get(`/api/nomina/recibos/${id}`)),
  // Devuelve un Blob para poder descargarlo con el token en la cabecera.
  reciboPdf: (id: number) => api.get(`/api/nomina/recibos/${id}/pdf`, { responseType: 'blob' }).then((r) => r.data as Blob),
};

export const reclutamientoApi = {
  vacantes: (estado?: string) => d(api.get('/api/vacantes', { params: { estado } })),
  postulaciones: (vacanteId: number) => d(api.get(`/api/vacantes/${vacanteId}/postulaciones`)),
  moverEtapa: (id: number, etapa: string) => d(api.patch(`/api/postulaciones/${id}/etapa`, { etapa })),
};

export const reportesApi = {
  headcount: () => d(api.get('/api/reportes/headcount')),
  asistencia: (desde: string, hasta: string) => d(api.get('/api/reportes/asistencia', { params: { desde, hasta } })),
  nomina: (periodoId: number) => d(api.get('/api/reportes/nomina', { params: { periodoId } })),
};

export const evaluacionesApi = {
  list: (params: { empleadoId?: number; periodo?: string }) => d(api.get('/api/evaluaciones', { params })),
  create: (body: any) => d(api.post('/api/evaluaciones', body)),
};
