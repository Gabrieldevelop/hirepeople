import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { nominaApi } from '../api/endpoints';
import { useAuth } from '../auth/AuthContext';
import { MESES, dinero, mensajeError } from '../util';

const esc = (t: unknown) => String(t ?? '').replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]!));

/** Abre el recibo en una ventana lista para imprimir o guardar como PDF (el PDF del servidor aún no existe). */
async function verRecibo(id: number, empleado?: string) {
  const r = await nominaApi.recibo(id);
  const conceptos = (r.conceptos ?? []) as any[];
  const filas = conceptos.map((c) =>
    `<tr><td>${esc(c.nombre)}</td><td>${esc(c.tipo)}</td><td style="text-align:right">${c.tipo === 'Descuento' ? '-' : ''}${dinero(c.monto)}</td></tr>`).join('');
  const w = window.open('', '_blank');
  if (!w) return;
  w.document.write(`<!doctype html><html lang="es"><head><meta charset="utf-8"><title>Recibo de nómina</title>
    <style>body{font-family:system-ui,sans-serif;max-width:640px;margin:2rem auto;color:#1c2b3a}table{width:100%;border-collapse:collapse}
    td,th{padding:.4rem;border-bottom:1px solid #ddd;text-align:left}h1{margin-bottom:0}.t{font-size:1.2rem;font-weight:700}</style></head><body>
    <h1>Recibo de nómina</h1><p>${empleado ? esc(empleado) + ' · ' : ''}${esc(MESES[r.periodo.mes])} ${esc(r.periodo.anio)} (${esc(r.periodo.tipo)})</p>
    <table><tr><td>Salario bruto</td><td></td><td style="text-align:right">${dinero(r.salarioBruto)}</td></tr>${filas}
    <tr><th>Total ingresos adicionales</th><th></th><th style="text-align:right">${dinero(r.totalIngresos)}</th></tr>
    <tr><th>Total descuentos</th><th></th><th style="text-align:right">-${dinero(r.totalDescuentos)}</th></tr></table>
    <p class="t">Salario neto: ${dinero(r.salarioNeto)}</p><script>window.onload=()=>window.print()</script></body></html>`);
  w.document.close();
}

export default function Nomina() {
  const { hasRole } = useAuth();
  const gestor = hasRole('RRHH', 'Admin');
  const qc = useQueryClient();
  const [periodoId, setPeriodoId] = useState<number | null>(null);
  const [error, setError] = useState('');

  const periodos = useQuery({ queryKey: ['periodos'], queryFn: nominaApi.periodos, enabled: gestor });
  const activo: number | undefined = periodoId ?? periodos.data?.[0]?.idPeriodo;
  const actual = (periodos.data ?? []).find((p: any) => p.idPeriodo === activo);
  const recibos = useQuery({
    queryKey: ['recibos', gestor, activo], enabled: gestor ? !!activo : true,
    queryFn: () => (gestor ? nominaApi.recibos(activo!) : nominaApi.misRecibos()),
  });
  const refrescar = () => { qc.invalidateQueries({ queryKey: ['recibos'] }); qc.invalidateQueries({ queryKey: ['periodos'] }); };
  const alFallar = (e: unknown) => setError(mensajeError(e, 'No se pudo completar la operación.'));
  const alExito = () => { setError(''); refrescar(); };

  // Los montos se calculan en el servidor; el frontend solo los muestra.
  const calcular = useMutation({ mutationFn: () => nominaApi.calcular(activo!), onSuccess: alExito, onError: alFallar });
  const cerrar = useMutation({ mutationFn: () => nominaApi.cerrar(activo!), onSuccess: alExito, onError: alFallar });
  const nuevo = useMutation({
    mutationFn: () => {
      const u = (periodos.data ?? [])[0];
      const base = u ? new Date(u.anio, u.mes, 1) : new Date();           // mes siguiente al último periodo
      const anio = base.getFullYear(), mes = base.getMonth() + 1;
      const ultimo = new Date(anio, mes, 0).getDate();
      return nominaApi.crearPeriodo({ anio, mes, tipo: 'Mensual', fechaPago: `${anio}-${String(mes).padStart(2, '0')}-${ultimo}` });
    },
    onSuccess: alExito, onError: alFallar,
  });

  const filas: any[] = recibos.data ?? [];
  const suma = (k: string) => filas.reduce((s, r) => s + (r[k] ?? 0), 0);
  const cerrado = actual?.estado === 'Cerrado';

  return (
    <>
      <h1>{gestor ? 'Nómina' : 'Mis recibos de nómina'}</h1>
      {gestor && (
        <div className="toolbar">
          <select value={activo ?? ''} onChange={(e) => setPeriodoId(Number(e.target.value))}>
            {(periodos.data ?? []).map((p: any) => <option key={p.idPeriodo} value={p.idPeriodo}>{MESES[p.mes]} {p.anio} · {p.tipo} · {p.estado}</option>)}
          </select>
          {hasRole('RRHH', 'Admin') && <button onClick={() => calcular.mutate()} disabled={!activo || cerrado || calcular.isPending}>Calcular nómina</button>}
          {hasRole('Admin') && <button className="ghost" onClick={() => cerrar.mutate()} disabled={!activo || cerrado}>Cerrar periodo</button>}
          <button className="ghost" onClick={() => nuevo.mutate()} disabled={nuevo.isPending}>Nuevo periodo</button>
        </div>
      )}
      {error && <p className="error">{error}</p>}
      {gestor && (
        <div className="grid4">
          <div className="card stat"><b>{filas.length}</b><span>Empleados liquidados</span></div>
          <div className="card stat"><b>{dinero(suma('salarioBruto'))}</b><span>Total bruto</span></div>
          <div className="card stat"><b>{dinero(suma('totalDescuentos'))}</b><span>Descuentos</span></div>
          <div className="card stat"><b>{dinero(suma('salarioNeto'))}</b><span>Total neto</span></div>
        </div>
      )}
      <div className="card table-wrap">
        <table>
          <thead><tr>{gestor ? <th>Empleado</th> : <th>Periodo</th>}<th>Salario bruto</th><th>Descuentos</th><th>Adicionales</th><th>Salario neto</th><th>Recibo</th></tr></thead>
          <tbody>
            {filas.map((r) => (
              <tr key={r.idRecibo}>
                <td>{gestor ? r.empleado : `${MESES[r.mes]} ${r.anio}`}</td><td>{dinero(r.salarioBruto)}</td><td>{dinero(r.totalDescuentos)}</td>
                <td>{dinero(r.totalIngresos)}</td><td>{dinero(r.salarioNeto)}</td>
                <td><button className="ghost" onClick={() => verRecibo(r.idRecibo, r.empleado)}>Ver / PDF</button></td>
              </tr>
            ))}
            {!recibos.isLoading && filas.length === 0 && (
              <tr><td colSpan={6} className="muted">{gestor ? 'Este periodo aún no tiene recibos. Use "Calcular nómina".' : 'Aún no tiene recibos.'}</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </>
  );
}
