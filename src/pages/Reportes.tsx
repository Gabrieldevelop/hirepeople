import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { reportesApi } from '../api/endpoints';
import { useAuth } from '../auth/AuthContext';
import { hoyLocal } from '../util';

const primerDiaMes = () => hoyLocal().slice(0, 8) + '01';

export default function Reportes() {
  const { hasRole } = useAuth();
  const [desde, setDesde] = useState(primerDiaMes());
  const [hasta, setHasta] = useState(hoyLocal());

  const headcount = useQuery({ queryKey: ['headcount'], queryFn: reportesApi.headcount, enabled: hasRole('RRHH', 'Admin') });
  const asistencia = useQuery({ queryKey: ['rep-asistencia', desde, hasta], queryFn: () => reportesApi.asistencia(desde, hasta) });

  const hc: any[] = headcount.data ?? [];
  const max = Math.max(1, ...hc.map((x) => x.total));

  return (
    <>
      <h1>Reportes</h1>
      {hasRole('RRHH', 'Admin') && (
        <div className="card">
          <h2>Empleados activos por departamento</h2>
          {hc.map((x) => (
            <div className="row" key={x.departamento} style={{ alignItems: 'center', gap: '1rem' }}>
              <span style={{ width: 160 }}>{x.departamento}</span>
              <div style={{ flex: 1, background: '#e3eaf0', borderRadius: 6 }}>
                <div style={{ width: `${(x.total / max) * 100}%`, background: 'var(--accent)', color: '#fff', borderRadius: 6, padding: '2px 8px', minWidth: 28 }}>{x.total}</div>
              </div>
            </div>
          ))}
        </div>
      )}
      <div className="card">
        <h2>Asistencia por estado</h2>
        <div className="toolbar">
          <label>Desde <input type="date" value={desde} onChange={(e) => setDesde(e.target.value)} /></label>
          <label>Hasta <input type="date" value={hasta} onChange={(e) => setHasta(e.target.value)} /></label>
        </div>
        <div className="grid4">
          {(asistencia.data ?? []).map((x: any) => (
            <div className="card stat" key={x.estado}><b>{x.total}</b><span>{x.estado}</span></div>
          ))}
        </div>
        {!asistencia.isLoading && (asistencia.data ?? []).length === 0 && <p className="muted">Sin marcas en el rango.</p>}
      </div>
    </>
  );
}
