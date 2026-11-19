package com.iptv4win.tv

import android.app.AlertDialog
import android.os.Bundle
import android.view.Gravity
import android.view.KeyEvent
import android.view.View
import android.view.ViewGroup
import android.widget.*
import androidx.appcompat.app.AppCompatActivity
import androidx.media3.common.MediaItem
import androidx.media3.common.Player
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.ui.PlayerView
import java.net.URI
import java.util.concurrent.Executors

class MainActivity : AppCompatActivity() {
    private val io = Executors.newSingleThreadExecutor()
    private val repo = IptvRepository()
    private var player: ExoPlayer? = null
    private var currentSource: PlaylistSource? = null
    private var channels = listOf<TvChannel>()
    private var shown = listOf<TvChannel>()
    private var selectedGroup = "Toutes"
    private lateinit var root: LinearLayout
    private lateinit var status: TextView
    private lateinit var channelList: ListView
    private lateinit var groupList: ListView
    private lateinit var video: PlayerView
    private lateinit var channelAdapter: ArrayAdapter<String>
    private lateinit var groupAdapter: ArrayAdapter<String>

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.decorView.systemUiVisibility = (View.SYSTEM_UI_FLAG_FULLSCREEN or View.SYSTEM_UI_FLAG_HIDE_NAVIGATION or View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY)
        player = ExoPlayer.Builder(this).build()
        buildUi()
        showWelcome()
    }

    private fun buildUi() {
        root = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL; setPadding(28, 18, 28, 18); setBackgroundColor(0xFF10131B.toInt()) }
        val header = LinearLayout(this).apply { gravity = Gravity.CENTER_VERTICAL }
        header.addView(TextView(this).apply { text = "IPTV4WIN TV"; textSize = 25f; setTextColor(0xFFD6B45C.toInt()); setPadding(0, 0, 28, 0) })
        header.addView(action("Playlists") { showWelcome() })
        header.addView(action("Contrôle parental") { parentalDialog() })
        header.addView(action("Météo") { featureMessage("Météo") })
        header.addView(action("Coran") { featureMessage("Coran") })
        root.addView(header, LinearLayout.LayoutParams(-1, 64))
        status = TextView(this).apply { text = "Ajoutez une playlist pour commencer."; textSize = 14f; setTextColor(0xFFB8C0CC.toInt()); setPadding(4, 8, 4, 8) }
        root.addView(status)
        val body = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
        groupAdapter = ArrayAdapter(this, android.R.layout.simple_list_item_1, mutableListOf("Toutes"))
        groupList = ListView(this).apply { adapter = groupAdapter; choiceMode = ListView.CHOICE_MODE_SINGLE; setBackgroundColor(0xFF171D29.toInt()) }
        groupList.onItemClickListener = AdapterView.OnItemClickListener { _, _, position, _ ->
            selectedGroup = groupAdapter.getItem(position) ?: "Toutes"; refreshChannels()
        }
        body.addView(groupList, LinearLayout.LayoutParams(210, -1))
        val center = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }
        video = PlayerView(this).apply { player = this@MainActivity.player; useController = true; setBackgroundColor(0xFF050608.toInt()) }
        center.addView(video, LinearLayout.LayoutParams(-1, 0, 1f))
        channelAdapter = ArrayAdapter(this, android.R.layout.simple_list_item_1, mutableListOf())
        channelList = ListView(this).apply { adapter = channelAdapter; setBackgroundColor(0xFF10131B.toInt()); choiceMode = ListView.CHOICE_MODE_SINGLE }
        channelList.onItemClickListener = AdapterView.OnItemClickListener { _, _, position, _ -> play(shown[position]) }
        center.addView(channelList, LinearLayout.LayoutParams(-1, 0, 1f))
        body.addView(center, LinearLayout.LayoutParams(0, -1, 1f))
        root.addView(body, LinearLayout.LayoutParams(-1, 0, 1f))
        setContentView(root)
        groupList.nextFocusRightId = View.NO_ID
        channelList.nextFocusLeftId = View.NO_ID
        groupList.isFocusable = true; channelList.isFocusable = true
        groupList.requestFocus()
    }

    private fun action(label: String, click: () -> Unit) = Button(this).apply {
        text = label; isFocusable = true; textSize = 13f; setOnClickListener { click() }
        setTextColor(0xFFFFFFFF.toInt())
    }

    private fun showWelcome() {
        val options = arrayOf("Ajouter une playlist", "Recharger la playlist actuelle")
        AlertDialog.Builder(this).setTitle("IPTV4Win TV").setItems(options) { _, which ->
            if (which == 0) addPlaylistDialog() else currentSource?.let(::loadSource) ?: addPlaylistDialog()
        }.setNegativeButton("Fermer", null).show()
    }

    private fun addPlaylistDialog() {
        val types = arrayOf("M3U", "Xtream", "Stalker")
        val type = Spinner(this).apply { adapter = ArrayAdapter(this@MainActivity, android.R.layout.simple_spinner_dropdown_item, types) }
        val name = edit("Nom de la playlist")
        val url = edit("URL du serveur / portail / fichier M3U")
        val user = edit("Nom d'utilisateur (Xtream)")
        val pass = edit("Mot de passe (Xtream)", true)
        val mac = edit("Adresse MAC (Stalker)")
        val serial = edit("Serial Number (facultatif)")
        val device = edit("Device ID (facultatif)")
        val device2 = edit("Device ID2 (facultatif)")
        val box = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL; setPadding(24, 8, 24, 0) }
        box.addView(type); listOf(name, url, user, pass, mac, serial, device, device2).forEach(box::addView)
        AlertDialog.Builder(this).setTitle("Nouvelle playlist").setView(ScrollView(this).apply { addView(box) })
            .setPositiveButton("Connecter") { _, _ ->
                val source = PlaylistSource(name.text.toString().ifBlank { "Ma playlist" }, types[type.selectedItemPosition],
                    url.text.toString().trim(), user.text.toString(), pass.text.toString(), mac.text.toString().trim(),
                    serial.text.toString().trim(), device.text.toString().trim(), device2.text.toString().trim())
                loadSource(source)
            }.setNegativeButton("Annuler", null).show()
    }

    private fun edit(hint: String, password: Boolean = false) = EditText(this).apply {
        this.hint = hint; textSize = 15f; isSingleLine = true
        if (password) inputType = android.text.InputType.TYPE_CLASS_TEXT or android.text.InputType.TYPE_TEXT_VARIATION_PASSWORD
    }

    private fun loadSource(source: PlaylistSource) {
        currentSource = source; status.text = "Chargement de la playlist…"
        io.execute {
            try {
                val loaded = repo.load(source)
                runOnUiThread {
                    channels = loaded
                    val groups = listOf("Toutes", "LIVE", "FILMS", "SERIES") + loaded.map { it.group }.filter { it.isNotBlank() }.distinct().sorted()
                    groupAdapter.clear(); groupAdapter.addAll(groups); groupAdapter.notifyDataSetChanged()
                    selectedGroup = "Toutes"; refreshChannels()
                    status.text = "${source.name} : ${loaded.size} éléments"
                    channelList.requestFocus()
                }
            } catch (e: Exception) { runOnUiThread { status.text = "Erreur : ${e.message}" } }
        }
    }

    private fun refreshChannels() {
        shown = channels.filter {
            when (selectedGroup) {
                "Toutes" -> true
                "LIVE" -> it.type == "Live"
                "FILMS" -> it.type == "Films"
                "SERIES" -> it.type == "Series"
                else -> it.group == selectedGroup
            }
        }
        channelAdapter.clear()
        channelAdapter.addAll(shown.map { "${it.number}.  ${it.name}" })
        channelAdapter.notifyDataSetChanged()
    }

    private fun play(channel: TvChannel) {
        if (isAdult(channel)) { parentalDialog(true); return }
        val source = currentSource ?: return
        status.text = "Connexion au flux : ${channel.name}"
        io.execute {
            try {
                val stream: String
                val headers: Map<String, String>
                if (channel.stalkerCmd.isNotBlank()) {
                    val resolved = repo.resolveStalker(source, channel.stalkerCmd)
                    stream = resolved.first; headers = resolved.second
                } else { stream = channel.streamUrl; headers = emptyMap() }
                require(stream.startsWith("http://") || stream.startsWith("https://") || stream.startsWith("rtsp://")) { "Adresse du flux invalide." }
                runOnUiThread {
                    val item = MediaItem.Builder().setUri(stream).setMediaMetadata(
                        androidx.media3.common.MediaMetadata.Builder().setTitle(channel.name).build()
                    ).build()
                    player?.setMediaItem(item)
                    player?.prepare()
                    player?.playWhenReady = true
                    status.text = "Lecture : ${channel.name}"
                }
            } catch (e: Exception) { runOnUiThread { status.text = "Échec de lecture : ${e.message}" } }
        }
    }

    private fun isAdult(c: TvChannel): Boolean =
        Regex("(^|[^a-z0-9])(adult|adulte|18\\+|xxx|porn|erotic|erotique|hot)([^a-z0-9]|$)", RegexOption.IGNORE_CASE)
            .containsMatchIn("${c.name} ${c.group}")

    private fun parentalDialog(blocking: Boolean = false) {
        val input = EditText(this).apply { hint = "Code parental"; inputType = android.text.InputType.TYPE_CLASS_NUMBER or android.text.InputType.TYPE_NUMBER_VARIATION_PASSWORD }
        AlertDialog.Builder(this).setTitle(if (blocking) "Contenu protégé" else "Contrôle parental")
            .setMessage(if (blocking) "Entrez le code parental pour continuer." else "Définissez un code PIN parental.")
            .setView(input).setPositiveButton("Valider") { _, _ ->
                val pin = input.text.toString()
                if (pin.length < 4) status.text = "Le code doit contenir au moins 4 chiffres."
                else status.text = "Le contrôle parental est prêt. La sauvegarde sécurisée du PIN sera finalisée dans la prochaine étape."
            }.setNegativeButton("Annuler", null).show()
    }

    private fun featureMessage(name: String) {
        AlertDialog.Builder(this).setTitle(name)
            .setMessage("Module ${name} : son intégration Android TV est prévue dans la migration. Les fonctions TV restent disponibles.")
            .setPositiveButton("OK", null).show()
    }

    override fun onKeyDown(keyCode: Int, event: KeyEvent?): Boolean {
        if (keyCode == KeyEvent.KEYCODE_MEDIA_PLAY_PAUSE) {
            val p = player ?: return super.onKeyDown(keyCode, event)
            if (p.isPlaying) p.pause() else p.play()
            return true
        }
        return super.onKeyDown(keyCode, event)
    }

    override fun onDestroy() {
        player?.release(); player = null
        io.shutdownNow()
        super.onDestroy()
    }
}
