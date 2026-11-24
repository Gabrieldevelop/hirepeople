import { FormEvent, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { empleadosApi, evaluacionesApi } from '../api/endpoints';
import { useAuth } from '../auth/AuthContext';
import { fecha, hoyLocal, mensajeError } from '../util';

const VACIO = { idEmpleado: '', periodo: `${new Date().getFullYear()}-S2`, puntaje: '4', comentarios: '' };

export default function Evaluaciones() {
  const { hasRole, user } = useAuth();
  const qc = useQueryClient();
  const [form, setForm] = useState(VACIO);
  const [error, setError] = useState('');

  const lista = useQuery({ queryKey: ['evaluaciones'], queryFn: () => evaluacionesApi.list({}) });
  // Solo el Jefe puede evaluar; elige entre los empleados de su equipo.
  const equipo = useQuery({ queryKey: ['equipo'], enabled: hasRole('Jefe'), queryFn: () => empleadosApi.list({ estado: 'Activo' }) });

  const crear = useMutation({
    mutationFn: () => evaluacionesApi.create({ ...form, idEmpleado: Number(form.idEmpleado), puntaje: Number(form.puntaje), fecha: hoyLocal() }),
    onSuccess: () => { setForm(VACIO); setError(''); qc.invalidateQueries({ queryKey: ['evaluaciones'] }); },
    onError: (e) => setError(mensajeError(e, 'No se pudo registrar la evaluación (solo el jefe directo puede evaluar).')),
  });
  const enviar = (e: FormEvent) => { e.preventDefault(); crear.mutate(); };
  const filas: any[] = lista.data ?? [];

  return (
    <>
      <h1>Evaluaciones del desempeño</h1>
      {hasRole('Jefe') && (
        <form className="card form2" onSubmit={enviar}>
          <label>Empleado
            <select required value={form.idEmpleado} onChange={(e) => setForm({ ...form, idEmpleado: e.target.value })}>
              <option value="">Seleccione…</option>
              {(equipo.data?.items ?? []).filter((e: any) => e.idEmpleado !== user?.idEmpleado)
                .map((e: any) => <option key={e.idEmpleado} value={e.idEmpleado}>{e.nombreCompleto}</option>)}
            </select>
          </label>
          <label>Periodo<input required value={form.periodo} onChange={(e) => setForm({ ...form, periodo: e.target.value })} /></label>
          <label>Puntaje (0 a 5)<input type="number" min="0" max="5" step="0.1" required value={form.puntaje} onChange={(e) => setForm({ ...form, puntaje: e.target.value })} /></label>
          <label>Comentarios<input value={form.comentarios} onChange={(e) => setForm({ ...form, comentarios: e.target.value })} /></label>
          <button type="submit" disabled={crear.isPending}>Registrar evaluación</button>
        </form>
      )}
      {error && <p className="error">{error}</p>}
      <div className="card table-wrap">
        <table>
          <thead><tr><th>Empleado</th><th>Periodo</th><th>Puntaje</th><th>Evaluador</th><th>Comentarios</th><th>Fecha</th></tr></thead>
          <tbody>
            {filas.map((e) => (
              <tr key={e.idEvaluacion}>
                <td>{e.empleado}</td><td>{e.periodo}</td><td>{e.puntaje} / 5</td><td>{e.evaluador}</td><td>{e.comentarios}</td><td>{fecha(e.fecha)}</td>
              </tr>
            ))}
            {!lista.isLoading && filas.length === 0 && <tr><td colSpan={6} className="muted">No hay evaluaciones registradas.</td></tr>}
          </tbody>
        </table>
      </div>
    </>
  );
}
