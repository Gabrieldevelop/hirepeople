import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { catalogosApi, empleadosApi } from '../api/endpoints';

export default function Empleados() {
  const [buscar, setBuscar] = useState('');
  const [departamento, setDepartamento] = useState('');
  const [estado, setEstado] = useState('Activo');
  const [pagina, setPagina] = useState(1);

  const deps = useQuery({ queryKey: ['departamentos'], queryFn: catalogosApi.departamentos });
  // Paginación, búsqueda y filtros se resuelven en el servidor (GET /api/empleados).
  const q = useQuery({
    queryKey: ['empleados', buscar, departamento, estado, pagina],
    queryFn: () => empleadosApi.list({ buscar, departamento, estado, pagina }),
  });
  const items: any[] = q.data?.items ?? [];

  return (
    <>
      <h1>Empleados</h1>
      <div className="toolbar">
        <input placeholder="Buscar por nombre o cédula" value={buscar} onChange={(e) => { setBuscar(e.target.value); setPagina(1); }} />
        <select value={departamento} onChange={(e) => { setDepartamento(e.target.value); setPagina(1); }}>
          <option value="">Departamento: Todos</option>
          {(deps.data ?? []).map((d: any) => <option key={d.id} value={d.id}>{d.nombre}</option>)}
        </select>
        <select value={estado} onChange={(e) => { setEstado(e.target.value); setPagina(1); }}>
          {['Activo', 'Vacaciones', 'Suspendido'].map((e) => <option key={e}>{e}</option>)}
        </select>
      </div>
      <div className="card table-wrap">
        <table>
          <thead><tr><th>Código</th><th>Nombre completo</th><th>Cargo</th><th>Departamento</th><th>Ingreso</th><th>Estado</th><th></th></tr></thead>
          <tbody>
            {items.map((e) => (
              <tr key={e.id}>
                <td>{e.codigo}</td><td>{e.nombres} {e.apellidos}</td><td>{e.cargo}</td><td>{e.departamento}</td>
                <td>{new Date(e.fechaIngreso).toLocaleDateString('es-DO')}</td>
                <td><span className={`pill ${e.estado}`}>{e.estado}</span></td>
                <td><Link to={`/empleados/${e.id}`}>Ver · Editar</Link></td>
              </tr>
            ))}
            {!q.isLoading && items.length === 0 && <tr><td colSpan={7} className="muted">Ningún empleado coincide con los filtros.</td></tr>}
          </tbody>
        </table>
      </div>
      <div className="pager">
        <span className="muted">Mostrando {items.length} de {q.data?.total ?? 0} empleados</span>
        <button className="ghost" disabled={pagina <= 1} onClick={() => setPagina(pagina - 1)}>Anterior</button>
        <span>{pagina} / {q.data?.totalPaginas ?? 1}</span>
        <button className="ghost" disabled={pagina >= (q.data?.totalPaginas ?? 1)} onClick={() => setPagina(pagina + 1)}>Siguiente</button>
      </div>
    </>
  );
}
