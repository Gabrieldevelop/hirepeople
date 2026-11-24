import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { reclutamientoApi } from '../api/endpoints';

const ETAPAS = ['Postulado', 'Entrevista', 'Prueba técnica', 'Contratado'];

export default function Reclutamiento() {
  const qc = useQueryClient();
  const [vacanteId, setVacanteId] = useState<number | null>(null);

  const vacantes = useQuery({ queryKey: ['vacantes'], queryFn: () => reclutamientoApi.vacantes('Publicada') });
  const activa = vacanteId ?? vacantes.data?.[0]?.id;
  // GET /api/vacantes/{id}/postulaciones llena cada columna del tablero.
  const post = useQuery({ queryKey: ['postulaciones', activa], enabled: !!activa, queryFn: () => reclutamientoApi.postulaciones(activa) });

  // PATCH /api/postulaciones/{id}/etapa mueve la tarjeta a otra etapa.
  const mover = useMutation({
    mutationFn: ({ id, etapa }: { id: number; etapa: string }) => reclutamientoApi.moverEtapa(id, etapa),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['postulaciones'] }),
  });

  const lista: any[] = post.data?.items ?? post.data ?? [];

  return (
    <>
      <h1>Reclutamiento</h1>
      <div className="toolbar">
        <select value={activa ?? ''} onChange={(e) => setVacanteId(Number(e.target.value))}>
          {(vacantes.data ?? []).map((v: any) => <option key={v.id} value={v.id}>{v.titulo}</option>)}
        </select>
      </div>
      <div className="board">
        {ETAPAS.map((et) => {
          const col = lista.filter((p) => p.etapa === et);
          return (
            <div className="col" key={et}
              onDragOver={(e) => e.preventDefault()}
              onDrop={(e) => mover.mutate({ id: Number(e.dataTransfer.getData('id')), etapa: et })}>
              <h2>{et} <span className="muted">{col.length}</span></h2>
              {col.map((p) => (
                <div className="card tarjeta" key={p.id} draggable onDragStart={(e) => e.dataTransfer.setData('id', String(p.id))}>
                  <b>{p.candidato}</b>
                  <span className="muted">{p.origen ?? ''}</span>
                </div>
              ))}
            </div>
          );
        })}
      </div>
    </>
  );
}
