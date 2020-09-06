using SyncSaber.SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SyncSaber
{
    public class PlaylistSong
    {
        public string hash;
        public string songName;

        public PlaylistSong(string hash, string songName)
        {
            this.hash = hash;
            this.songName = songName;
        }
    }

    public class PlaylistIO
    {

        public static Playlist ReadPlaylistSongs(Playlist playlist)
        {
            try
            {
                string playlistPath = $"Playlists\\{playlist.fileName}{(playlist.oldFormat ? ".json" : ".bplist")}";
                String json = File.ReadAllText(playlistPath);

                JSONNode playlistNode = JSON.Parse(json);

                playlist.Image = playlistNode["image"];
                playlist.Title = playlistNode["playlistTitle"];
                playlist.Author = playlistNode["playlistAuthor"];
                playlist.Songs = new List<PlaylistSong>();

                foreach (JSONNode node in playlistNode["songs"].AsArray)
                {
                    playlist.Songs.Add(new PlaylistSong(node["hash"], node["songName"]));
                }

                playlist.fileLoc = null;

                return playlist;
            }
            catch (Exception e)
            {
                Logger.Info($"Exception parsing playlist: {e}");
            }
            return null;
        }

        public static void WritePlaylist(Playlist playlist)
        {
            JSONNode playlistNode = new JSONObject();

            playlistNode.Add("playlistTitle", new JSONString(playlist.Title));
            playlistNode.Add("playlistAuthor", new JSONString(playlist.Author));
            playlistNode.Add("image", new JSONString(playlist.Image));

            JSONArray songArray = new JSONArray();
            try
            {
                foreach (PlaylistSong s in playlist.Songs)
                {
                    JSONObject songObject = new JSONObject();
                    songObject.Add("hash", new JSONString(s.hash));
                    songObject.Add("songName", new JSONString(s.songName));
                    songArray.Add(songObject);
                }
                playlistNode.Add("songs", songArray);
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }

            playlistNode.Add("fileLoc", new JSONString("1"));

            if (!Directory.Exists("Playlists")) Directory.CreateDirectory("Playlists");
            File.WriteAllText($"Playlists\\{playlist.fileName}{(playlist.oldFormat ? ".json" : ".bplist")}", playlistNode.ToString());
        }
    }

    public class Playlist
    {
        public string Title;
        public string Author;
        public string Image;
        public List<PlaylistSong> Songs;
        public string fileLoc;
        public string fileName;
        public bool oldFormat = true;

        public Playlist(string playlistFileName, string playlistTitle, string playlistAuthor, string image)
        {
            this.fileName = playlistFileName;
            this.Title = playlistTitle;
            this.Author = playlistAuthor;
            this.Image = image;
            Songs = new List<PlaylistSong>();
            fileLoc = "";

            ReadPlaylist();
        }

        public void Add(string hash, string songName)
        {
            Songs.Add(new PlaylistSong(hash, songName));
        }

        public void WritePlaylist()
        {
            PlaylistIO.WritePlaylist(this);
        }

        public bool ReadPlaylist()
        {
            string oldPlaylistPath = $"Playlists\\{this.fileName}.json";
            string newPlaylistPath = $"Playlists\\{this.fileName}.bplist";
            oldFormat = !File.Exists(newPlaylistPath);
            Logger.Info($"Playlist \"{Title}\" found in {(oldFormat?"old":"new")} playlist format.");

            string playlistPath = oldFormat ? oldPlaylistPath : newPlaylistPath;
            if (File.Exists(playlistPath))
            {
                var playlist = PlaylistIO.ReadPlaylistSongs(this);
                if (playlist != null)
                {
                    this.Title = playlist.Title;
                    this.Author = playlist.Author;
                    this.Image = playlist.Image;
                    this.Songs = playlist.Songs;
                    this.fileLoc = playlist.fileLoc;
                    Logger.Info("Success loading playlist!");
                    return true;
                }
            }
            return false;
        }
    }
}