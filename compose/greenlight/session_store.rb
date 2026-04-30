# Dev-only override mounted over Greenlight v3's config/initializers/session_store.rb.
#
# The stock initializer sets secure: Rails.env.production? on the session
# cookie. The image runs with RAILS_ENV=production, so the cookie gets the
# Secure flag and browsers refuse to store it over plain HTTP. No session
# cookie => no CSRF token in the session => /auth/openid_connect rejects
# the form submit with InvalidAuthenticityToken => Greenlight redirects
# to /auth/failure, which the SPA renders as "Page Not Found".
#
# Forcing secure: false here lets the cookie through. Production stacks
# already use HTTPS terminators and will not see this override.

Rails.application.config.session_store :cookie_store,
                                       key: '_greenlight-3_0_session',
                                       secure: false,
                                       path: ENV.fetch('RELATIVE_URL_ROOT', '/')
