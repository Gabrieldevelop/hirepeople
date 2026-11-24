import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { asistenciaApi, empleadosApi } from '../api/endpoints';
import { useAuth } from '../auth/AuthContext';
import { hora, hoyLocal, mensajeError } from '../util';

export default function Asistencia() {
  const [dia, setDia] = useState(hoyLocal());
  const { hasRole } = useAuth();
  const qc = useQueryClient();

  // Tabla diaria: GET /api/asistencia?fecha=YYYY-MM-DD
  const tabla = useQuery({ queryKey: ['asistencia', dia], queryFn: () => asistenciaApi.porFecha(dia) });
  // Empleados en vacaciones (solo lo ven RR. HH. y jefes).
  const vacaciones = useQuery({
    queryKey: ['empleados-vacaciones'], enabled: hasRole('RRHH', 'Jefe', 'Admin'),
    queryFn: () => empleadosApi.list({ estado: 'Vacaciones' }), retry: false,
  });
  // Registrar mi entrada/salida: POST /api/asistencia/marcas
  const marcar = useMutation({ mutationFn: asistenciaApi.marcar, onSuccess: () => qc.invalidateQueries({ queryKey: ['asistencia'] }) });

  const filas: any[] = tabla.data ?? [];
  const indicadores: [string, any][] = [
    ['Presentes', filas.filter((a) => a.horaEntrada).length],
    ['Tardanzas', filas.filter((a) => a.estado === 'Tardanza').length],
    ['Ausentes', filas.filter((a) => a.estado === 'Ausente').length],
    ['En vacaciones', vacaciones.data?.total ?? '—'],
  ];

  return (
    <>
      <h1>Control de asistencia</h1>
      <div className="toolbar">
        <input type="date" value={dia} onChange={(e) => setDia(e.target.value)} />
        {hasRole('Empleado') && <button onClick={() => marcar.mutate()} disabled={marcar.isPending}>Marcar entrada o salida</button>}
      </div>
      {marcar.isError && <p className="error">{mensajeError(marcar.error, 'No se pudo registrar la marca.')}</p>}
      {marcar.isSuccess && <p className="ok">Marca registrada.</p>}
      <div className="grid4">
        {indicadores.map(([t, v]) => (
          <div className="card stat" key={t}><b>{v}</b><span>{t}</span></div>
        ))}
      </div>
      <div className="card table-wrap">
        <table>
          <thead><tr><th>Empleado</th><th>Departamento</th><th>Entrada</th><th>Salida</th><th>Horas</th><th>Estado</th></tr></thead>
          <tbody>
            {filas.map((a) => (
              <tr key={a.idRegistro}>
                <td>{a.empleado}</td><td>{a.departamento}</td><td>{hora(a.horaEntrada)}</td>
                <td>{hora(a.horaSalida)}</td><td>{a.horasTrabajadas ?? '—'}</td>
                <td><span className={`pill ${a.estado === 'Puntual' ? 'Activo' : a.estado === 'Tardanza' ? 'Pendiente' : 'Rechazada'}`}>{a.estado}</span></td>
              </tr>
            ))}
            {!tabla.isLoading && filas.length === 0 && <tr><td colSpan={6} className="muted">No hay marcas para esta fecha.</td></tr>}
          </tbody>
        </table>
      </div>
    </>
  );
}
