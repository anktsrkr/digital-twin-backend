import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { LogtoProvider } from '@logto/react';
import './styles/index.css';
import App from './App.tsx';

const config = {
  endpoint: import.meta.env.VITE_LOGTO_ENDPOINT || 'https://tenant.logto.app',
  appId: import.meta.env.VITE_LOGTO_APP_ID || 'local_spa_app_id',
  resources: [import.meta.env.VITE_LOGTO_API_RESOURCE || 'api://digital.twin'],
  scopes: ['openid', 'profile', 'email', 'offline_access']
};

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <LogtoProvider config={config}>
      <App />
    </LogtoProvider>
  </StrictMode>,
);
