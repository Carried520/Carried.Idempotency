local key = KEYS[1]
local fingerprint = ARGV[1]
local ownerToken = ARGV[2]
local leaseDurationMs = tonumber(ARGV[3])

local time = redis.call("TIME")
local nowMs =
    (tonumber(time[1]) * 1000) +
    math.floor(tonumber(time[2]) / 1000)

local expiresAt = nowMs + leaseDurationMs

local function acquire()
    redis.call(
        "HSET",
        key,
        "state", "in_progress",
        "fingerprint", fingerprint,
        "owner_token", ownerToken,
        "expires_at", expiresAt
    )

    redis.call("HDEL", key, "payload")
    redis.call("PEXPIRE", key, leaseDurationMs)

    return { 0 }
end

if redis.call("EXISTS", key) == 0 then
    return acquire()
end

local entry = redis.call(
    "HMGET",
    key,
    "state",
    "fingerprint",
    "expires_at"
)

local state = entry[1]
local storedFingerprint = entry[2]
local storedExpiresAt = tonumber(entry[3])

if not state or not storedFingerprint or not storedExpiresAt then
    return redis.error_reply("Invalid idempotency entry")
end

if state ~= "in_progress" and state ~= "completed" then
    return redis.error_reply("Invalid idempotency entry state")
end

if storedExpiresAt <= nowMs then
    return acquire()
end

if storedFingerprint ~= fingerprint then
    return { 3 }
end

if state == "in_progress" then
    return { 1 }
end

local payload = redis.call("HGET", key, "payload")

if not payload then
    return redis.error_reply(
        "Completed idempotency entry has no payload"
    )
end

return { 2, payload }