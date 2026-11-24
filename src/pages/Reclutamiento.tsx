import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { reclutamientoApi } from '../api/endpoints';
import { mensajeError } from '../util';

const ETAPAS = ['Postulado', 'Entrevista', 'Prueba técnica', 'Contratado', 'Descartado'];

export default function Reclutamiento() {
  const qc = useQueryClient();
  const [vacanteId, setVacanteId] = useState<number | null>(null);

  const vacantes = useQuery({ queryKey: ['vacantes'], queryFn: () => reclutamientoApi.vacantes('Publicada') });
  const activa = vacanteId ?? vacantes.data?.[0]?.idVacante;
  // GET /api/vacantes/{id}/postulaciones llega agrupado por etapa: [{ etapa, items: [...] }]
  const post = useQuery({ queryKey: ['postulaciones', activa], enabled: !!activa, queryFn: () => reclutamientoApi.postulaciones(activa) });

  // PATCH /api/postulaciones/{id}/etapa mueve la tarjeta a otra etapa.
  const mover = useMutation({
    mutationFn: ({ id, etapa }: { id: number; etapa: string }) => reclutamientoApi.moverEtapa(id, etapa),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['postulaciones'] }),
  });

  const lista: any[] = (post.data ?? []).flatMap((g: any) => g.items ?? []);

  return (
    <>
      <h1>Reclutamiento</h1>
      <div className="toolbar">
        <select value={activa ?? ''} onChange={(e) => setVacanteId(Number(e.target.value))}>
          {(vacantes.data ?? []).map((v: any) => <option key={v.idVacante} value={v.idVacante}>{v.titulo}</option>)}
        </select>
      </div>
      {mover.isError && <p className="error">{mensajeError(mover.error, 'No se pudo mover la postulación.')}</p>}
      {!vacantes.isLoading && (vacantes.data ?? []).length === 0 && <p className="muted">No hay vacantes publicadas.</p>}
      <div className="board" style={{ gridTemplateColumns: `repeat(${ETAPAS.length}, minmax(190px, 1fr))` }}>
        {ETAPAS.map((et) => {
          const col = lista.filter((p) => p.etapa === et);
          return (
            <div className="col" key={et}
              onDragOver={(e) => e.preventDefault()}
              onDrop={(e) => {
                const id = Number(e.dataTransfer.getData('id'));
                if (id && lista.find((p) => p.idPostulacion === id)?.etapa !== et) mover.mutate({ id, etapa: et });
              }}>
              <h2>{et} <span className="muted">{col.length}</span></h2>
              {col.map((p) => (
                <div className="card tarjeta" key={p.idPostulacion} draggable onDragStart={(e) => e.dataTransfer.setData('id', String(p.idPostulacion))}>
                  <b>{p.candidato}</b>
                  <span className="muted">{p.correo}</span>
                  {p.calificacion != null && <span className="muted">Calificación: {p.calificacion} / 5</span>}
                </div>
              ))}
            </div>
          );
        })}
      </div>
    </>
  );
}
