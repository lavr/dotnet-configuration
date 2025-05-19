Примеры реп:
- https://github.com/domaindrivendev/Swashbuckle.AspNetCore/
- https://github.com/dotnet/runtime/
- https://github.com/andrewlock/NetEscapades.Configuration


Пример темплейта:
```
{{ global.logging.common | to_yaml  }}
ConnectionStrings:
  Db1: {{ PostgresConnection { database: "dbname1", server: "postgres01" } }}
  Db2: {{ PostgresConnection { database: "dbname2" } }}
SomeApiSettings:
  Host: {{ global.public_url }}/sorting
Queue1:
{{ global.queues.queues | to_yaml | indent(2)  }}
```

Пример values.yaml:
```
global:
  public_url: https://app.corp.tld
  databases:
    postgres01:
      host: postgres.corp.tld
      port: "6432"
      user: postgres-user
      password: postgres-password
  queues:
    rmq01:
      Host: rabbitmq.corp.tld
      VirtualHost: ETH
      User: rmquser
      Зassword: rmqpassword
  logging:
    common:
      ElasticApm: {}
```
