var LabServer = {};

LabServer.getSessionStorage = function (key) {
    return sessionStorage.getItem(key);
}

LabServer.setSessionStorage = function (key, data) {
    return sessionStorage.setItem(key, data);
}