import { FormEvent, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ausenciasApi, catalogosApi, empleadosApi } from '../api/endpoints';
import { useAuth } from '../auth/AuthContext';
import { fecha, mensajeError } from '../util';

const VACIO = { idTipo: '', fechaInicio: '', fechaFin: '', motivo: '' };

export default function Ausencias() {
  const { user, hasRole } = useAuth();
  const qc = useQueryClient();
  const [form, setForm] = useState(VACIO);
  const [error, setError] = useState('');
  const puedeDecidir = hasRole('RRHH', 'Jefe', 'Admin');

  const tipos = useQuery({ queryKey: ['tipos'], queryFn: catalogosApi.tiposAusencia });
  const lista = useQuery({ queryKey: ['ausencias'], queryFn: () => ausenciasApi.list({}) });
  const saldo = useQuery({
    queryKey: ['saldo', user?.idEmpleado], enabled: !!user?.idEmpleado,
    queryFn: () => empleadosApi.saldoVacaciones(user!.idEmpleado!), retry: false,
  });
  const refrescar = () => { qc.invalidateQueries({ queryKey: ['ausencias'] }); qc.invalidateQueries({ queryKey: ['saldo'] }); };

  // POST /api/ausencias: el servidor valida saldo y solapamientos, y calcula los días hábiles.
  const crear = useMutation({
    mutationFn: () => ausenciasApi.create({ ...form, idTipo: Number(form.idTipo) }),
    onSuccess: () => { setForm(VACIO); setError(''); refrescar(); },
    onError: (e) => setError(mensajeError(e, 'No se pudo enviar la solicitud.')),
  });

  // PATCH /api/ausencias/{id}/aprobacion: el comentario es obligatorio al rechazar.
  const decidir = async (id: number, aprobada: boolean) => {
    const comentario = aprobada ? '' : window.prompt('Escriba el motivo del rechazo') ?? '';
    if (!aprobada && !comentario.trim()) return;
    try {
      await ausenciasApi.decidir(id, aprobada, comentario);
      setError('');
    } catch (e) {
      setError(mensajeError(e, 'No se pudo registrar la decisión.'));
    }
    refrescar();
  };
  const cancelar = async (id: number) => {
    try { await ausenciasApi.cancelar(id); setError(''); } catch (e) { setError(mensajeError(e, 'No se pudo cancelar.')); }
    refrescar();
  };

  const enviar = (e: FormEvent) => { e.preventDefault(); crear.mutate(); };
  const filas: any[] = lista.data ?? [];
  const esMia = (s: any) => s.idEmpleado === user?.idEmpleado;

  return (
    <>
      <h1>Vacaciones y permisos</h1>
      <form className="card form2" onSubmit={enviar}>
        <label>Tipo de ausencia
          <select required value={form.idTipo} onChange={(e) => setForm({ ...form, idTipo: e.target.value })}>
            <option value="">Seleccione…</option>
            {(tipos.data ?? []).map((t: any) => <option key={t.idTipo} value={t.idTipo}>{t.nombre}</option>)}
          </select>
        </label>
        <label>Desde<input type="date" required value={form.fechaInicio} onChange={(e) => setForm({ ...form, fechaInicio: e.target.value })} /></label>
        <label>Hasta<input type="date" required value={form.fechaFin} onChange={(e) => setForm({ ...form, fechaFin: e.target.value })} /></label>
        <label>Motivo<input value={form.motivo} onChange={(e) => setForm({ ...form, motivo: e.target.value })} /></label>
        <button type="submit" disabled={crear.isPending}>Enviar solicitud</button>
      </form>
      {error && <p className="error">{error}</p>}
      {crear.isSuccess && !error && <p className="ok">Solicitud enviada.</p>}
      {saldo.data && <p className="muted">Saldo de vacaciones {saldo.data.anio}: {saldo.data.diasDisponibles} días disponibles · {saldo.data.diasUsados} usados</p>}
      <div className="card table-wrap">
        <table>
          <thead><tr><th>Empleado</th><th>Tipo</th><th>Desde</th><th>Hasta</th><th>Días</th><th>Estado</th><th>Decisión</th></tr></thead>
          <tbody>
            {filas.map((s) => (
              <tr key={s.idSolicitud}>
                <td>{s.empleado}</td><td>{s.tipo}</td><td>{fecha(s.fechaInicio)}</td><td>{fecha(s.fechaFin)}</td><td>{s.diasSolicitados}</td>
                <td><span className={`pill ${s.estado}`}>{s.estado}</span></td>
                <td>
                  {s.estado === 'Pendiente' && puedeDecidir && !esMia(s) && (<>
                    <button className="ghost" onClick={() => decidir(s.idSolicitud, true)}>Aprobar</button>{' '}
                    <button className="ghost" onClick={() => decidir(s.idSolicitud, false)}>Rechazar</button>
                  </>)}
                  {s.estado === 'Pendiente' && esMia(s) && <button className="ghost" onClick={() => cancelar(s.idSolicitud)}>Cancelar</button>}
                </td>
              </tr>
            ))}
            {!lista.isLoading && filas.length === 0 && <tr><td colSpan={7} className="muted">No hay solicitudes.</td></tr>}
          </tbody>
        </table>
      </div>
    </>
  );
}
