import axios from 'axios';

// La URL base viene de la variable de entorno VITE_API_URL (ver .env.example).
export const api = axios.create({ baseURL: import.meta.env.VITE_API_URL });

const KEYS = { access: 'rrhh_access', refresh: 'rrhh_refresh' };

/** "Mantener sesión iniciada" => localStorage; si no, sessionStorage. */
export const tokens = {
  get: (k: 'access' | 'refresh') => localStorage.getItem(KEYS[k]) ?? sessionStorage.getItem(KEYS[k]),
  set(access: string, refresh: string, persist: boolean) {
    const s = persist ? localStorage : sessionStorage;
    s.setItem(KEYS.access, access);
    s.setItem(KEYS.refresh, refresh);
  },
  clear() {
    [localStorage, sessionStorage].forEach((s) => Object.values(KEYS).forEach((k) => s.removeItem(k)));
  },
};

// Adjunta el token de acceso (JWT) a cada petición.
api.interceptors.request.use((cfg) => {
  const t = tokens.get('access');
  if (t) cfg.headers.Authorization = `Bearer ${t}`;
  return cfg;
});

// Si la API responde 401, intenta POST /api/auth/refresh una sola vez y repite la petición.
let refreshing: Promise<string> | null = null;
api.interceptors.response.use(
  (r) => r,
  async (error) => {
    const original = error.config;
    const refreshToken = tokens.get('refresh');
    if (error.response?.status === 401 && !original._retry && refreshToken) {
      original._retry = true;
      try {
        refreshing ??= axios
          .post(`${import.meta.env.VITE_API_URL}/api/auth/refresh`, { refreshToken })
          .then((r) => {
            const persist = !!localStorage.getItem(KEYS.refresh);
            tokens.set(r.data.accessToken, r.data.refreshToken ?? refreshToken, persist);
            return r.data.accessToken as string;
          })
          .finally(() => (refreshing = null));
        original.headers.Authorization = `Bearer ${await refreshing}`;
        return api(original);
      } catch {
        tokens.clear();
        window.location.href = '/login';
      }
    }
    return Promise.reject(error);
  }
);
