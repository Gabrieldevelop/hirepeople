import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { useMutation, useQuery } from '@tanstack/react-query';
import { empleadosApi } from '../api/endpoints';

export default function EmpleadoDetalle() {
  const { id = '' } = useParams();
  const [form, setForm] = useState<any>({});
  const [sensibles, setSensibles] = useState<any>(null);

  // GET /api/empleados/{id}: la ficha llega sin salario ni cuenta bancaria.
  const ficha = useQuery({ queryKey: ['empleado', id], queryFn: () => empleadosApi.get(id) });
  useEffect(() => { if (ficha.data) setForm(ficha.data); }, [ficha.data]);

  // PUT /api/empleados/{id}
  const guardar = useMutation({ mutationFn: () => empleadosApi.update(id, form) });

  // Los datos sensibles se piden bajo demanda: cada lectura queda auditada en el backend.
  const verSensibles = async () => setSensibles(await empleadosApi.datosSensibles(id));

  const campo = (k: string, label: string, readOnly = false) => (
    <label>{label}
      <input value={form[k] ?? ''} readOnly={readOnly} onChange={(e) => setForm({ ...form, [k]: e.target.value })} />
    </label>
  );

  return (
    <>
      <h1>{form.nombres} {form.apellidos}</h1>
      <p className="muted">{form.cargo} · {form.departamento} · {form.codigo}</p>
      <div className="card form2">
        {campo('nombres', 'Nombres')}{campo('apellidos', 'Apellidos')}
        {campo('correo', 'Correo')}{campo('telefono', 'Teléfono')}
        {campo('cedula', 'Cédula (enmascarada)', true)}{campo('departamento', 'Departamento', true)}
      </div>
      <div className="card">
        <h2>Datos sensibles · acceso auditado</h2>
        <p>Salario base: <b>{sensibles?.salario ?? '••••••••'}</b> · Cuenta bancaria: <b>{sensibles?.cuentaBancaria ?? '•••• •••• ••••'}</b></p>
        <button className="ghost" onClick={verSensibles}>Ver datos sensibles</button>
      </div>
      <div className="actions">
        <button onClick={() => guardar.mutate()} disabled={guardar.isPending}>Guardar cambios</button>
        <button className="ghost" onClick={() => setForm(ficha.data)}>Cancelar</button>
        {guardar.isSuccess && <span className="ok">Cambios guardados.</span>}
        {guardar.isError && <span className="error">No se pudieron guardar los cambios.</span>}
      </div>
    </>
  );
}
