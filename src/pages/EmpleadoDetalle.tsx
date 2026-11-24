import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { useMutation, useQuery } from '@tanstack/react-query';
import { empleadosApi } from '../api/endpoints';
import { useAuth } from '../auth/AuthContext';
import { dinero, fecha, mensajeError } from '../util';

export default function EmpleadoDetalle() {
  const { id = '' } = useParams();
  const { hasRole } = useAuth();
  const puedeEditar = hasRole('RRHH', 'Admin');
  const [form, setForm] = useState<any>({});
  const [sensibles, setSensibles] = useState<any>(null);

  // GET /api/empleados/{id}: la ficha llega sin salario y con la cédula enmascarada.
  const ficha = useQuery({ queryKey: ['empleado', id], queryFn: () => empleadosApi.get(id) });
  const contratos = useQuery({ queryKey: ['contratos', id], queryFn: () => empleadosApi.contratos(id) });
  const saldo = useQuery({ queryKey: ['saldo', id], queryFn: () => empleadosApi.saldoVacaciones(id), retry: false });
  const evals = useQuery({ queryKey: ['evals', id], queryFn: () => empleadosApi.evaluaciones(id), retry: false });
  useEffect(() => { if (ficha.data) setForm(ficha.data); }, [ficha.data]);

  // PUT /api/empleados/{id}. La cédula va vacía: el servidor la conserva si no se envía una nueva.
  const guardar = useMutation({
    mutationFn: () => empleadosApi.update(id, {
      codigo: form.codigo, cedula: form.cedulaNueva ?? '', nombres: form.nombres, apellidos: form.apellidos,
      fechaNacimiento: form.fechaNacimiento, correo: form.correo, idDepartamento: form.idDepartamento,
      idCargo: form.idCargo, fechaIngreso: form.fechaIngreso,
    }),
  });

  // Los datos sensibles se piden bajo demanda: cada lectura queda auditada en el backend.
  const verSensibles = async () => setSensibles(await empleadosApi.datosSensibles(id));

  const campo = (k: string, label: string, readOnly = false, type = 'text') => (
    <label>{label}
      <input type={type} value={form[k] ?? ''} readOnly={readOnly || !puedeEditar} onChange={(e) => setForm({ ...form, [k]: e.target.value })} />
    </label>
  );

  return (
    <>
      <h1>{form.nombres} {form.apellidos}</h1>
      <p className="muted">{form.cargo} · {form.departamento} · {form.codigo}</p>
      <div className="card form2">
        {campo('nombres', 'Nombres')}{campo('apellidos', 'Apellidos')}
        {campo('correo', 'Correo')}{campo('fechaNacimiento', 'Fecha de nacimiento', false, 'date')}
        {campo('cedulaMascarada', 'Cédula (enmascarada)', true)}{campo('departamento', 'Departamento', true)}
      </div>
      {hasRole('RRHH', 'Admin') && (
        <div className="card">
          <h2>Datos sensibles · acceso auditado</h2>
          <p>Salario base: <b>{sensibles ? dinero(sensibles.salarioBase) : '••••••••'}</b></p>
          <button className="ghost" onClick={verSensibles}>Ver datos sensibles</button>
        </div>
      )}
      {puedeEditar && (
        <div className="actions">
          <button onClick={() => guardar.mutate()} disabled={guardar.isPending}>Guardar cambios</button>
          <button className="ghost" onClick={() => setForm(ficha.data)}>Cancelar</button>
          {guardar.isSuccess && <span className="ok">Cambios guardados.</span>}
          {guardar.isError && <span className="error">{mensajeError(guardar.error, 'No se pudieron guardar los cambios.')}</span>}
        </div>
      )}

      <div className="card table-wrap" style={{ marginTop: '1rem' }}>
        <h2 style={{ padding: '1rem 1rem 0' }}>Contratos</h2>
        <table>
          <thead><tr><th>Tipo</th><th>Inicio</th><th>Fin</th><th>Estado</th></tr></thead>
          <tbody>
            {(contratos.data ?? []).map((c: any) => (
              <tr key={c.idContrato}><td>{c.tipo}</td><td>{fecha(c.fechaInicio)}</td><td>{c.fechaFin ? fecha(c.fechaFin) : 'Indefinido'}</td>
                <td><span className={`pill ${c.activo ? 'Activo' : ''}`}>{c.activo ? 'Vigente' : 'Inactivo'}</span></td></tr>
            ))}
          </tbody>
        </table>
      </div>

      {saldo.data && (
        <p className="muted">Vacaciones {saldo.data.anio}: {saldo.data.diasDisponibles} días disponibles · {saldo.data.diasUsados} usados de {saldo.data.diasAsignados}</p>
      )}

      {(evals.data ?? []).length > 0 && (
        <div className="card table-wrap">
          <h2 style={{ padding: '1rem 1rem 0' }}>Evaluaciones</h2>
          <table>
            <thead><tr><th>Periodo</th><th>Puntaje</th><th>Comentarios</th><th>Fecha</th></tr></thead>
            <tbody>
              {evals.data.map((e: any) => (
                <tr key={e.idEvaluacion}><td>{e.periodo}</td><td>{e.puntaje} / 5</td><td>{e.comentarios}</td><td>{fecha(e.fecha)}</td></tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
