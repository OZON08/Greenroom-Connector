# Dev-only override mounted over Greenlight v3's config/initializers/omniauth.rb.
#
# The stock initializer enables OIDC discovery and uses one URL (the issuer) for
# both the browser-facing redirect AND Greenlight's own backchannel calls. That
# does not work for the local Docker stack: the browser must reach Keycloak via
# 'localhost' (no hosts-file edit), while Greenlight (in a container) reaches it
# via the Docker network alias 'keycloak'.
#
# This override disables discovery and sets each endpoint explicitly, so the
# authorization_endpoint and end_session_endpoint can use the public URL while
# token, userinfo and jwks calls go through the Docker DNS name.
#
# Two ENV vars drive it:
#   OPENID_CONNECT_ISSUER         — public URL, e.g. http://localhost:8080/realms/greenlight
#                                   (must match the 'iss' claim Keycloak signs into tokens)
#   OPENID_CONNECT_BACKEND_BASE   — Docker-internal URL, e.g. http://keycloak:8080/realms/greenlight
#                                   (used for token, userinfo, jwks and discovery calls)
#
# If OPENID_CONNECT_BACKEND_BASE is unset, falls back to OPENID_CONNECT_ISSUER
# so production deployments are unaffected.

Rails.application.config.middleware.use OmniAuth::Builder do
  issuer       = ENV.fetch('OPENID_CONNECT_ISSUER', '')
  backend_base = ENV.fetch('OPENID_CONNECT_BACKEND_BASE', issuer)

  if issuer.present?
    provider :openid_connect,
             issuer: issuer,
             scope: %i[openid email profile],
             uid_field: ENV.fetch('OPENID_CONNECT_UID_FIELD', 'sub'),
             discovery: false,
             client_options: {
               identifier: ENV.fetch('OPENID_CONNECT_CLIENT_ID'),
               secret: ENV.fetch('OPENID_CONNECT_CLIENT_SECRET'),
               redirect_uri: File.join(ENV.fetch('OPENID_CONNECT_REDIRECT', ''),
                                       'auth', 'openid_connect', 'callback'),
               authorization_endpoint: File.join(issuer, 'protocol', 'openid-connect', 'auth'),
               token_endpoint:        File.join(backend_base, 'protocol', 'openid-connect', 'token'),
               userinfo_endpoint:     File.join(backend_base, 'protocol', 'openid-connect', 'userinfo'),
               jwks_uri:              File.join(backend_base, 'protocol', 'openid-connect', 'certs'),
               end_session_endpoint:  File.join(issuer, 'protocol', 'openid-connect', 'logout')
             }
  end
end
