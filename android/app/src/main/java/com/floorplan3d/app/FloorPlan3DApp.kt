package com.floorplan3d.app

import android.app.Application
import android.content.SharedPreferences
import android.content.Context

class FloorPlan3DApp : Application() {

    companion object {
        lateinit var prefs: SharedPreferences
            private set

        const val PREF_SERVER_URL = "server_url"
        const val DEFAULT_SERVER  = "http://192.168.1.50:8000"
    }

    override fun onCreate() {
        super.onCreate()
        prefs = getSharedPreferences("FloorPlan3D_Prefs", Context.MODE_PRIVATE)
    }

    val serverUrl: String
        get() = prefs.getString(PREF_SERVER_URL, DEFAULT_SERVER) ?: DEFAULT_SERVER
}
