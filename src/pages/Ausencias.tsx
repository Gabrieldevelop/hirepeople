import { FormEvent, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ausenciasApi, catalogosApi, empleadosApi } from '../api/endpoints';
import { useAuth } from '../auth/AuthContext';

export default function Ausencias() {
  const { user, hasRole } = useAuth();
  const qc = useQueryClient();
  const [form, setForm] = useState({ idTipo: '', fechaInicio: '', fechaFin: '', motivo: '' });
  const puedeDecidir = hasRole('RRHH', 'Jefe');

  const tipos = useQuery({ queryKey: ['tipos'], queryFn: catalogosApi.tiposAusencia });
  const lista = useQuery({ queryKey: ['ausencias'], queryFn: () => ausenciasApi.list({}) });
  const saldo = useQuery({
    queryKey: ['saldo', user?.idEmpleado], enabled: !!user?.idEmpleado,
    queryFn: () => empleadosApi.saldoVacaciones(user!.idEmpleado!),
  });
  const refrescar = () => qc.invalidateQueries({ queryKey: ['ausencias'] });

  // POST /api/ausencias: el servidor valida saldo y calcula los días hábiles.
  const crear = useMutation({ mutationFn: () => ausenciasApi.create(form), onSuccess: refrescar });

  // PATCH /api/ausencias/{id}/aprobacion: el comentario es obligatorio al rechazar.
  const decidir = async (id: number, aprobada: boolean) => {
    const comentario = aprobada ? '' : window.prompt('Escriba el motivo del rechazo') ?? '';
    if (!aprobada && !comentario.trim()) return;
    await ausenciasApi.decidir(id, aprobada, comentario);
    refrescar();
  };

  const enviar = (e: FormEvent) => { e.preventDefault(); crear.mutate(); };
  const filas: any[] = lista.data?.items ?? lista.data ?? [];

  return (
    <>
      <h1>Vacaciones y permisos</h1>
      <form className="card form2" onSubmit={enviar}>
        <label>Tipo de ausencia
          <select required value={form.idTipo} onChange={(e) => setForm({ ...form, idTipo: e.target.value })}>
            <option value="">Seleccione…</option>
            {(tipos.data ?? []).map((t: any) => <option key={t.id} value={t.id}>{t.nombre}</option>)}
          </select>
        </label>
        <label>Desde<input type="date" required value={form.fechaInicio} onChange={(e) => setForm({ ...form, fechaInicio: e.target.value })} /></label>
        <label>Hasta<input type="date" required value={form.fechaFin} onChange={(e) => setForm({ ...form, fechaFin: e.target.value })} /></label>
        <label>Motivo<input value={form.motivo} onChange={(e) => setForm({ ...form, motivo: e.target.value })} /></label>
        <button type="submit" disabled={crear.isPending}>Enviar solicitud</button>
        {crear.isError && <span className="error">{(crear.error as any).response?.data?.mensaje ?? 'No se pudo enviar la solicitud.'}</span>}
      </form>
      {saldo.data && <p className="muted">Saldo de vacaciones: {saldo.data.disponibles} días disponibles · {saldo.data.usados} usados</p>}
      <div className="card table-wrap">
        <table>
          <thead><tr><th>Empleado</th><th>Tipo</th><th>Estado</th><th>Decisión</th></tr></thead>
          <tbody>
            {filas.map((s) => (
              <tr key={s.id}>
                <td>{s.empleado}</td><td>{s.tipo}</td><td><span className={`pill ${s.estado}`}>{s.estado}</span></td>
                <td>
                  {s.estado === 'Pendiente' && puedeDecidir && (<>
                    <button className="ghost" onClick={() => decidir(s.id, true)}>Aprobar</button>{' '}
                    <button className="ghost" onClick={() => decidir(s.id, false)}>Rechazar</button>
                  </>)}
                  {s.estado === 'Pendiente' && !puedeDecidir && <button className="ghost" onClick={async () => { await ausenciasApi.cancelar(s.id); refrescar(); }}>Cancelar</button>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}
