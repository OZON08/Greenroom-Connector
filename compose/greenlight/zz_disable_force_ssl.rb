# Dev-only override mounted into the Greenlight container.
# Greenlight v3 hardcodes config.force_ssl = true in config/environments/production.rb.
# That redirects every plain HTTP request to HTTPS, which we don't want for the
# local Docker stack (no cert, no proxy).
#
# Initializers run after environment configuration, and the "zz_" prefix makes
# this load last alphabetically, so it wins over any prior force_ssl setting.

Rails.application.config.force_ssl = false
