## Назначение программы как видит его автор форка

Автоматическое создание личных бэкапов данных с моего личного NAS Synology в облаке Яндекс.Диск, где компания Яндекс зарезала скорость по WebDAV, чтобы якобы не использовали для коммерческих целей. При этом поддержка утверждает, что это проблемы клиентов (всех), а их сервер WebDAV ничего не ограничивает.

## Подход

NAS { Backup Manager -> WebDAV клиент } -> WebDAVMailRuCloud { WebDAV сервер -> Эмуляция API Web-браузера -> Яндекс.Диск }

Особенности:
- Полностью автоматическая (скриптовая) работа
- Для этого используются TOTP пароли
- Учетные данные Яндекс.Диск передаются они в Логине и Пароле WebDAV (Логин - почта yandex, пароль - "пароль@@@TOTP-Secret-without-spaces")

## Отличия этого форка от оригинала

1. Исправлено удаление в Яндекс.Диск (работает, но не идеально)
2. Добавлена поддержка TOTP кодов для авторизации в Яндекс.Диск (работает)
3. Docker-образ для использования в NAS

## Как установить в Synology
1. Установить **Container Manager**
2. Создать каталог для контейнера _/volume1/docker/webdavmailrucloud_
3. Создать **Проект** в **Container Manager** с созданными каталогом в качестве _Пути_, в качестве _Источника_ указать _Создать docker-compose.yml_ со следующим содержимым:
```
services:
  webdavserver:
    image: ghcr.io/harmonyblend/webdavmailrucloud:latest
    container_name: webdavmailrucloud
    ports:
      - 10801:80
    restart: always
```
где _10801_ - свободный порт в Synology.

4. В качестве назначения резервного копирования в **Hyper Backup** указать:
- WebDAV-сервер = _http://localhost:10801_
- логин = login@yandex.ru
- пароль = пароль_учетной_записи@@@секретный_код_TOTP

Способ подтверждения входа в **Яндекс.Диск** должен быть _Пароль+Одноразовые_коды(Яндекс.Ключ)_. _Секретный_код_TOTP_ можно получить, если во время перехода на одноразовые пароли TOTP выбрать ручной ввод.
   
