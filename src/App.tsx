import { Navigate, Route, Routes } from 'react-router-dom';
import { useAuth } from './auth/AuthContext';
import Layout from './components/Layout';
import Login from './pages/Login';
import Dashboard from './pages/Dashboard';
import Empleados from './pages/Empleados';
import EmpleadoDetalle from './pages/EmpleadoDetalle';
import Asistencia from './pages/Asistencia';
import Ausencias from './pages/Ausencias';
import Nomina from './pages/Nomina';
import Reclutamiento from './pages/Reclutamiento';
import Evaluaciones from './pages/Evaluaciones';
import Reportes from './pages/Reportes';

const Pronto = ({ t }: { t: string }) => (
  <div className="card"><h1>{t}</h1><p className="muted">Este módulo pertenece a la Fase 2 o 3 del proyecto.</p></div>
);

/** Rutas protegidas: sin sesión se redirige a /login. */
function Protegida() {
  const { user, loading } = useAuth();
  if (loading) return <p className="muted pad">Cargando…</p>;
  return user ? <Layout /> : <Navigate to="/login" replace />;
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route element={<Protegida />}>
        <Route path="/panel" element={<Dashboard />} />
        <Route path="/empleados" element={<Empleados />} />
        <Route path="/empleados/:id" element={<EmpleadoDetalle />} />
        <Route path="/asistencia" element={<Asistencia />} />
        <Route path="/ausencias" element={<Ausencias />} />
        <Route path="/nomina" element={<Nomina />} />
        <Route path="/reclutamiento" element={<Reclutamiento />} />
        <Route path="/evaluaciones" element={<Evaluaciones />} />
        <Route path="/reportes" element={<Reportes />} />
        <Route path="/configuracion" element={<Pronto t="Configuración" />} />
      </Route>
      <Route path="*" element={<Navigate to="/panel" replace />} />
    </Routes>
  );
}
