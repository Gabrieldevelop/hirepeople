# Sistema de Gestión de Recursos Humanos · Frontend

React 18 + Vite + TypeScript, React Router, TanStack Query y Axios.

## Puesta en marcha
1. `npm install`
2. Copie `.env.example` a `.env` y configure `VITE_API_URL` (URL base de la API .NET).
3. `npm run dev`

## Estructura
- `src/api/client.ts`: Axios, JWT en cada petición y renovación automática con `POST /api/auth/refresh`.
- `src/api/endpoints.ts`: un método por endpoint de la referencia de API del PDF.
- `src/auth/AuthContext.tsx`: sesión y roles (`GET /api/auth/me`).
- `src/components/Layout.tsx`: menú lateral filtrado por rol.
- `src/pages/`: Login, Panel, Empleados, Ficha, Asistencia, Ausencias, Nómina y Reclutamiento.

## Supuestos sobre las respuestas de la API
El PDF define rutas y roles, pero no el JSON exacto. Se asumió: login → `{ accessToken, refreshToken }`;
`/api/auth/me` → `{ nombre, correo, roles[], idEmpleado }`; listados paginados → `{ items, total, totalPaginas }`.
Los nombres de campo están en las páginas, por si el backend usa otros.
