import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { asistenciaApi, ausenciasApi, reportesApi } from '../api/endpoints';
import { useAuth } from '../auth/AuthContext';
import { hoyLocal } from '../util';

export default function Dashboard() {
  const { hasRole } = useAuth();
  const gestor = hasRole('RRHH', 'Admin');

  // Indicadores: /api/reportes/headcount (por departamento), /api/asistencia (marcas de hoy) y ausencias pendientes.
  const headcount = useQuery({ queryKey: ['headcount'], queryFn: reportesApi.headcount, enabled: gestor });
  const marcas = useQuery({ queryKey: ['asistencia', hoyLocal()], queryFn: () => asistenciaApi.porFecha(hoyLocal()) });
  const pendientes = useQuery({ queryKey: ['ausencias', 'Pendiente'], queryFn: () => ausenciasApi.list({ estado: 'Pendiente' }) });

  const total = Array.isArray(headcount.data) ? headcount.data.reduce((s: number, x: any) => s + (x.total ?? 0), 0) : '—';
  const filas: any[] = marcas.data ?? [];
  const lista: any[] = pendientes.data ?? [];
  const presentes = marcas.data ? filas.filter((m) => m.horaEntrada).length : '—';
  const tardanzas = marcas.data ? filas.filter((m) => m.estado === 'Tardanza').length : '—';

  return (
    <>
      <h1>Panel general</h1>
      <div className="grid4">
        <Link to="/empleados" className="card stat"><b>{total}</b><span>Empleados activos</span></Link>
        <Link to="/ausencias" className="card stat"><b>{lista.length}</b><span>Ausencias pendientes</span></Link>
        <Link to="/asistencia" className="card stat"><b>{presentes}</b><span>Presentes hoy</span></Link>
        <Link to="/asistencia" className="card stat"><b>{tardanzas}</b><span>Tardanzas hoy</span></Link>
      </div>
      <div className="card">
        <h2>Solicitudes pendientes</h2>
        {lista.length === 0 && <p className="muted">No hay solicitudes por revisar.</p>}
        {lista.slice(0, 5).map((s) => (
          <div className="row" key={s.idSolicitud}>
            <span>{s.empleado} · {s.tipo} · {s.diasSolicitados} días</span>
            <Link to="/ausencias">Revisar</Link>
          </div>
        ))}
      </div>
    </>
  );
}
