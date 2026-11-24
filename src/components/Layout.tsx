import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { Rol, useAuth } from '../auth/AuthContext';

// Cada módulo declara qué roles lo ven (según la sección "Roles de acceso" del PDF).
const MENU: { to: string; label: string; roles: Rol[] }[] = [
  { to: '/panel', label: 'Panel general', roles: ['Admin', 'RRHH', 'Jefe', 'Empleado'] },
  { to: '/empleados', label: 'Empleados', roles: ['Admin', 'RRHH', 'Jefe'] },
  { to: '/asistencia', label: 'Asistencia', roles: ['RRHH', 'Jefe', 'Empleado'] },
  { to: '/ausencias', label: 'Ausencias', roles: ['RRHH', 'Jefe', 'Empleado'] },
  { to: '/nomina', label: 'Nómina', roles: ['Admin', 'RRHH', 'Empleado'] },
  { to: '/reclutamiento', label: 'Reclutamiento', roles: ['RRHH'] },
  { to: '/evaluaciones', label: 'Evaluaciones', roles: ['RRHH', 'Jefe'] },
  { to: '/reportes', label: 'Reportes', roles: ['Admin', 'RRHH', 'Jefe'] },
  { to: '/configuracion', label: 'Configuración', roles: ['Admin'] },
];

export default function Layout() {
  const { user, logout, hasRole } = useAuth();
  const nav = useNavigate();
  const iniciales = (user?.nombre ?? '?').split(' ').map((p) => p[0]).slice(0, 2).join('');
  return (
    <div className="shell">
      <aside className="side">
        <div className="brand">RR. HH.</div>
        <nav>
          {MENU.filter((m) => hasRole(...m.roles)).map((m) => (
            <NavLink key={m.to} to={m.to}>{m.label}</NavLink>
          ))}
        </nav>
      </aside>
      <div className="main">
        <header className="top">
          <span>{user?.nombre} · {user?.roles.join(', ')}</span>
          <span className="avatar">{iniciales}</span>
          <button className="ghost" onClick={async () => { await logout(); nav('/login'); }}>Salir</button>
        </header>
        <main className="content"><Outlet /></main>
      </div>
    </div>
  );
}
