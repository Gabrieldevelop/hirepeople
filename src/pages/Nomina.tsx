import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { nominaApi } from '../api/endpoints';
import { useAuth } from '../auth/AuthContext';

const dinero = (n: number) => (n ?? 0).toLocaleString('es-DO', { minimumFractionDigits: 2 });

export default function Nomina() {
  const { hasRole } = useAuth();
  const qc = useQueryClient();
  const [periodoId, setPeriodoId] = useState<number | null>(null);

  const periodos = useQuery({ queryKey: ['periodos'], queryFn: nominaApi.periodos });
  const activo = periodoId ?? periodos.data?.[0]?.id;
  const recibos = useQuery({ queryKey: ['recibos', activo], enabled: !!activo, queryFn: () => nominaApi.recibos(activo) });
  const refrescar = () => { qc.invalidateQueries({ queryKey: ['recibos'] }); qc.invalidateQueries({ queryKey: ['periodos'] }); };

  // Los montos se calculan en el servidor; el frontend solo los muestra.
  const calcular = useMutation({ mutationFn: () => nominaApi.calcular(activo), onSuccess: refrescar });
  const cerrar = useMutation({ mutationFn: () => nominaApi.cerrar(activo), onSuccess: refrescar });

  // Descarga el PDF con el token en la cabecera y lo abre desde un Blob.
  const verPdf = async (id: number) => window.open(URL.createObjectURL(await nominaApi.reciboPdf(id)), '_blank');

  const filas: any[] = recibos.data?.items ?? recibos.data ?? [];
  const suma = (k: string) => filas.reduce((s, r) => s + (r[k] ?? 0), 0);

  return (
    <>
      <h1>Nómina</h1>
      <div className="toolbar">
        <select value={activo ?? ''} onChange={(e) => setPeriodoId(Number(e.target.value))}>
          {(periodos.data ?? []).map((p: any) => <option key={p.id} value={p.id}>{p.nombre ?? `${p.mes}/${p.anio}`} · {p.estado}</option>)}
        </select>
        {hasRole('RRHH') && <button onClick={() => calcular.mutate()} disabled={!activo || calcular.isPending}>Calcular nómina</button>}
        {hasRole('Admin') && <button className="ghost" onClick={() => cerrar.mutate()} disabled={!activo}>Cerrar periodo</button>}
      </div>
      <div className="grid4">
        <div className="card stat"><b>{filas.length}</b><span>Empleados liquidados</span></div>
        <div className="card stat"><b>{dinero(suma('salarioBruto'))}</b><span>Total bruto</span></div>
        <div className="card stat"><b>{dinero(suma('totalDescuentos'))}</b><span>Descuentos</span></div>
        <div className="card stat"><b>{dinero(suma('salarioNeto'))}</b><span>Total neto</span></div>
      </div>
      <div className="card table-wrap">
        <table>
          <thead><tr><th>Empleado</th><th>Salario bruto</th><th>Descuentos</th><th>Adicionales</th><th>Salario neto</th><th>Recibo</th></tr></thead>
          <tbody>
            {filas.map((r) => (
              <tr key={r.id}>
                <td>{r.empleado}</td><td>{dinero(r.salarioBruto)}</td><td>{dinero(r.totalDescuentos)}</td>
                <td>{dinero(r.totalIngresos)}</td><td>{dinero(r.salarioNeto)}</td>
                <td><button className="ghost" onClick={() => verPdf(r.id)}>PDF</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}
