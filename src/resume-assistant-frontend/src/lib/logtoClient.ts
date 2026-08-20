/**
 * Logto Passwordless Magic Link & Authentication Client
 * Connects to Logto Cloud / Local Logto service and .NET 10 backend endpoints.
 */

export interface LogtoConfig {
  endpoint: string;
  appId: string;
  resources?: string[];
  scopes?: string[];
}

export interface MagicLinkResponse {
  success: boolean;
  message: string;
  email?: string;
  inferred_company?: string;
  expires_in_seconds?: number;
  preview_token?: string;
  magic_link_url?: string;
}

export interface VerifyTokenResponse {
  valid: boolean;
  user_id?: string;
  email?: string;
  inferred_company?: string;
  message?: string;
  access_token?: string;
}

export interface RecruiterSession {
  email: string;
  company?: string;
  userId?: string;
  token?: string;
  authenticatedAt: string;
}

const STORAGE_KEY_SESSION = 'recruiter_session';
const STORAGE_KEY_EMAIL = 'recruiter_email';
const STORAGE_KEY_COMPANY = 'recruiter_company';

export const logtoConfig: LogtoConfig = {
  endpoint: import.meta.env.VITE_LOGTO_ENDPOINT || 'https://tenant.logto.app',
  appId: import.meta.env.VITE_LOGTO_APP_ID || 'local_spa_app_id',
  resources: [import.meta.env.VITE_LOGTO_API_RESOURCE || 'https://api.resumetwin.local'],
  scopes: ['openid', 'profile', 'email', 'offline_access']
};

export const isLogtoConfigured = Boolean(
  import.meta.env.VITE_LOGTO_ENDPOINT &&
  !import.meta.env.VITE_LOGTO_ENDPOINT.includes('YOUR_') &&
  import.meta.env.VITE_LOGTO_APP_ID &&
  !import.meta.env.VITE_LOGTO_APP_ID.includes('YOUR_')
);

const getApiBaseUrl = (): string => {
  if (typeof window !== 'undefined' && window.location.hostname === 'localhost') {
    return 'http://localhost:5000';
  }
  return import.meta.env.VITE_BACKEND_API_URL || import.meta.env.VITE_API_URL || 'http://localhost:5000';
};

/**
 * Dispatches a Logto passwordless magic link to the recruiter's email.
 */
export async function requestMagicLink(email: string, redirectUri?: string): Promise<MagicLinkResponse> {
  const apiUrl = `${getApiBaseUrl()}/api/auth/magic-link`;
  const defaultRedirect = typeof window !== 'undefined' ? `${window.location.origin}${window.location.pathname}` : 'http://localhost:5173';

  const res = await fetch(apiUrl, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      email: email.trim(),
      redirect_uri: redirectUri || defaultRedirect
    })
  });

  const data: MagicLinkResponse = await res.json();
  if (!res.ok && !data.message) {
    throw new Error(`Failed to dispatch magic link (${res.status})`);
  }

  return data;
}

/**
 * Verifies a 6-digit one-time token or link token against the Logto authentication service.
 */
export async function verifyOneTimeToken(email: string, token: string): Promise<VerifyTokenResponse> {
  const apiUrl = `${getApiBaseUrl()}/api/auth/verify-token`;

  const res = await fetch(apiUrl, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      email: email.trim(),
      token: token.trim()
    })
  });

  const data: VerifyTokenResponse = await res.json();
  if (!res.ok && !data.message) {
    throw new Error(`Token verification failed (${res.status})`);
  }

  return data;
}

/**
 * Retrieves saved session from localStorage.
 */
export function getSavedRecruiterSession(): RecruiterSession | null {
  if (typeof window === 'undefined') return null;

  try {
    const raw = localStorage.getItem(STORAGE_KEY_SESSION);
    if (raw) {
      return JSON.parse(raw);
    }
  } catch {}

  const email = localStorage.getItem(STORAGE_KEY_EMAIL);
  const company = localStorage.getItem(STORAGE_KEY_COMPANY);
  if (email) {
    return {
      email,
      company: company || undefined,
      authenticatedAt: new Date().toISOString()
    };
  }

  return null;
}

/**
 * Stores active recruiter session in localStorage.
 */
export function saveRecruiterSession(session: RecruiterSession): void {
  if (typeof window === 'undefined') return;

  try {
    localStorage.setItem(STORAGE_KEY_SESSION, JSON.stringify(session));
    localStorage.setItem(STORAGE_KEY_EMAIL, session.email);
    if (session.company) {
      localStorage.setItem(STORAGE_KEY_COMPANY, session.company);
    } else {
      localStorage.removeItem(STORAGE_KEY_COMPANY);
    }
    if (session.token) {
      localStorage.setItem('recruiter_token', session.token);
    }
  } catch {}
}

/**
 * Clears active recruiter session on sign-out.
 */
export function clearRecruiterSession(): void {
  if (typeof window === 'undefined') return;

  try {
    localStorage.removeItem(STORAGE_KEY_SESSION);
    localStorage.removeItem(STORAGE_KEY_EMAIL);
    localStorage.removeItem(STORAGE_KEY_COMPANY);
    localStorage.removeItem('recruiter_token');
  } catch {}
}
