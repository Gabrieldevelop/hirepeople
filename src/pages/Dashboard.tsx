import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { asistenciaApi, ausenciasApi, reportesApi } from '../api/endpoints';

const hoy = () => new Date().toISOString().slice(0, 10);

export default function Dashboard() {
  // Los indicadores salen de /api/reportes/headcount y /api/asistencia/resumen.
  const headcount = useQuery({ queryKey: ['headcount'], queryFn: reportesApi.headcount });
  const resumen = useQuery({ queryKey: ['resumen', hoy()], queryFn: () => asistenciaApi.resumen(hoy(), hoy()) });
  // Las solicitudes pendientes: GET /api/ausencias?estado=Pendiente
  const pendientes = useQuery({ queryKey: ['ausencias', 'Pendiente'], queryFn: () => ausenciasApi.list({ estado: 'Pendiente' }) });

  const hc = headcount.data;
  const total = hc?.total ?? (Array.isArray(hc) ? hc.reduce((s: number, x: any) => s + (x.cantidad ?? 0), 0) : '—');
  const lista: any[] = pendientes.data?.items ?? pendientes.data ?? [];

  return (
    <>
      <h1>Panel general</h1>
      <div className="grid4">
        <Link to="/empleados" className="card stat"><b>{total}</b><span>Empleados activos</span></Link>
        <Link to="/ausencias" className="card stat"><b>{lista.length}</b><span>Ausencias pendientes</span></Link>
        <Link to="/asistencia" className="card stat"><b>{resumen.data?.presentes ?? '—'}</b><span>Presentes hoy</span></Link>
        <Link to="/asistencia" className="card stat"><b>{resumen.data?.tardanzas ?? '—'}</b><span>Tardanzas hoy</span></Link>
      </div>
      <div className="card">
        <h2>Solicitudes pendientes</h2>
        {lista.length === 0 && <p className="muted">No hay solicitudes por revisar.</p>}
        {lista.slice(0, 5).map((s) => (
          <div className="row" key={s.id}>
            <span>{s.empleado} · {s.tipo} · {s.dias} días</span>
            <Link to="/ausencias">Revisar</Link>
          </div>
        ))}
      </div>
    </>
  );
}
