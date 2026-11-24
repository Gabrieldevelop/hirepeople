import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { asistenciaApi } from '../api/endpoints';

export default function Asistencia() {
  const [fecha, setFecha] = useState(new Date().toISOString().slice(0, 10));
  const qc = useQueryClient();

  // Tabla diaria: GET /api/asistencia?fecha=YYYY-MM-DD ; indicadores: GET /api/asistencia/resumen
  const tabla = useQuery({ queryKey: ['asistencia', fecha], queryFn: () => asistenciaApi.porFecha(fecha) });
  const resumen = useQuery({ queryKey: ['resumen', fecha], queryFn: () => asistenciaApi.resumen(fecha, fecha) });
  // Registrar mi entrada/salida: POST /api/asistencia/marcas
  const marcar = useMutation({ mutationFn: asistenciaApi.marcar, onSuccess: () => qc.invalidateQueries({ queryKey: ['asistencia'] }) });

  const r = resumen.data ?? {};
  const filas: any[] = tabla.data?.items ?? tabla.data ?? [];

  return (
    <>
      <h1>Control de asistencia</h1>
      <div className="toolbar">
        <input type="date" value={fecha} onChange={(e) => setFecha(e.target.value)} />
        <button onClick={() => marcar.mutate()} disabled={marcar.isPending}>Marcar entrada o salida</button>
      </div>
      <div className="grid4">
        {[['Presentes', r.presentes], ['Tardanzas', r.tardanzas], ['Ausentes', r.ausentes], ['En vacaciones', r.vacaciones]].map(([t, v]) => (
          <div className="card stat" key={t as string}><b>{v ?? '—'}</b><span>{t}</span></div>
        ))}
      </div>
      <div className="card table-wrap">
        <table>
          <thead><tr><th>Empleado</th><th>Departamento</th><th>Entrada</th><th>Salida</th><th>Horas</th><th>Estado</th></tr></thead>
          <tbody>
            {filas.map((a, i) => (
              <tr key={a.id ?? i}>
                <td>{a.empleado}</td><td>{a.departamento}</td><td>{a.horaEntrada ?? '—'}</td>
                <td>{a.horaSalida ?? '—'}</td><td>{a.horasTrabajadas ?? '—'}</td><td>{a.estado}</td>
              </tr>
            ))}
            {!tabla.isLoading && filas.length === 0 && <tr><td colSpan={6} className="muted">No hay marcas para esta fecha.</td></tr>}
          </tbody>
        </table>
      </div>
    </>
  );
}
