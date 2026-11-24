import { FormEvent, useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

export default function Login() {
  const { user, login } = useAuth();
  const nav = useNavigate();
  const [correo, setCorreo] = useState('');
  const [password, setPassword] = useState('');
  const [persist, setPersist] = useState(false);
  const [error, setError] = useState('');

  if (user) return <Navigate to="/panel" replace />;

  // Envía POST /api/auth/login; el backend bloquea la cuenta tras cinco intentos fallidos.
  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    try {
      await login(correo, password, persist);
      nav('/panel');
    } catch (err: any) {
      setError(err.response?.data?.mensaje ?? 'No se pudo iniciar sesión. Revise su correo y contraseña.');
    }
  };

  return (
    <div className="login">
      <form className="card" onSubmit={submit}>
        <div className="brand dark">RR. HH.</div>
        <h1>Sistema de Gestión de Recursos Humanos</h1>
        <label>Correo electrónico<input type="email" required value={correo} onChange={(e) => setCorreo(e.target.value)} /></label>
        <label>Contraseña<input type="password" required value={password} onChange={(e) => setPassword(e.target.value)} /></label>
        <label className="check"><input type="checkbox" checked={persist} onChange={(e) => setPersist(e.target.checked)} /> Mantener sesión iniciada</label>
        {error && <p className="error">{error}</p>}
        <button type="submit">Ingresar</button>
      </form>
    </div>
  );
}
