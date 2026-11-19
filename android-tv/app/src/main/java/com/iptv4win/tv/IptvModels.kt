package com.iptv4win.tv

data class PlaylistSource(
    val name: String,
    val type: String,
    val url: String,
    val username: String = "",
    val password: String = "",
    val mac: String = "",
    val serial: String = "",
    val deviceId: String = "",
    val deviceId2: String = ""
)

data class TvChannel(
    val number: Int,
    val name: String,
    val group: String = "",
    val type: String = "Live",
    val streamUrl: String = "",
    val stalkerCmd: String = ""
)
