import {
  UserManager,
  WebStorageStateStore,
  type User,
  // Log,
} from 'oidc-client-ts';
import { Platform } from '@/models/platform';

// Log.setLogger(console);
// Log.setLevel(Log.DEBUG);

export const extractToken = (user: User | null): string | null =>
  user !== null ? user.access_token : null;

export const parseJwt = (token: string) =>
  JSON.parse(Buffer.from(token.split('.')[1], 'base64').toString());

export const userManager = new UserManager({
  authority: import.meta.env.VITE_API_BASE_URL,
  scope: 'openid offline_access user_api',
  client_id: 'crpg-web-ui',
  redirect_uri: window.location.origin + '/signin-callback',
  silent_redirect_uri: window.location.origin + '/signin-silent-callback',
  post_logout_redirect_uri: window.location.origin,
  response_type: 'code',
  userStore: new WebStorageStateStore({ store: window.localStorage }),
});

export const getUser = () => userManager.getUser();

export const login = (platform: Platform) =>
  userManager.signinRedirect({
    extraQueryParams: {
      identity_provider: platform,
    },
  });

export const logout = () => userManager.signoutRedirect();

export const getToken = async () => extractToken(await userManager.getUser());
