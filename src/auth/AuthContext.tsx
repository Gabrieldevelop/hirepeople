import { createContext, useContext, useEffect, useState, ReactNode } from 'react';
import { authApi } from '../api/endpoints';
import { tokens } from '../api/client';

export type Rol = 'Admin' | 'RRHH' | 'Jefe' | 'Empleado';
export interface Usuario { nombre: string; correo: string; roles: string[]; idEmpleado?: number }

interface Ctx {
  user: Usuario | null;
  loading: boolean;
  login: (correo: string, password: string, persist: boolean) => Promise<void>;
  logout: () => Promise<void>;
  hasRole: (...r: Rol[]) => boolean;
}
const AuthCtx = createContext<Ctx>(null!);
export const useAuth = () => useContext(AuthCtx);

/** Carga el usuario con GET /api/auth/me; sus roles definen qué módulos se muestran. */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<Usuario | null>(null);
  const [loading, setLoading] = useState(!!tokens.get('access'));

  useEffect(() => {
    if (!tokens.get('access')) return;
    authApi.me().then(setUser).catch(() => tokens.clear()).finally(() => setLoading(false));
  }, []);

  const login: Ctx['login'] = async (correo, password, persist) => {
    const r = await authApi.login(correo, password);
    tokens.set(r.accessToken, r.refreshToken, persist);
    setUser(await authApi.me());
  };
  const logout = async () => {
    try { await authApi.logout(tokens.get('refresh') ?? ''); } catch { /* se limpia igual */ }
    tokens.clear();
    setUser(null);
  };
  const hasRole = (...r: Rol[]) => !!user?.roles.some((x) => r.includes(x as Rol));

  return <AuthCtx.Provider value={{ user, loading, login, logout, hasRole }}>{children}</AuthCtx.Provider>;
}
