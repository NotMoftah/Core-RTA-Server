using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization;
using System.Text.Json;
using MongoDB.Driver;
using MongoDB.Bson;
using System;


namespace CoreRTA.Database
{

    public static class RtaDb
    {
        public static string DatabaseName { get; private set; }
        public static string EndPoint { get; private set; }

        private static MongoClient dbClient;
        private static IMongoDatabase database;



        static RtaDb()
        {
            DatabaseName = "CoreRtsApp";
            EndPoint = "mongodb://localhost:27017/";
        }

        public static void Connect(string dbName)
        {
            DatabaseName = dbName;
            EndPoint = "mongodb://localhost:27017/";

            dbClient = new MongoClient(EndPoint);
            database = dbClient.GetDatabase(DatabaseName);
        }

        public static void Connect(string dbName, string remoteEnd)
        {
            EndPoint = remoteEnd;
            DatabaseName = dbName;

            dbClient = new MongoClient(EndPoint);
            database = dbClient.GetDatabase(DatabaseName);
        }


        #region Collections and Mapping

        public static void AddClassToMap(Type classType)
        {
            if (!BsonClassMap.IsClassMapRegistered(classType))
            {
                BsonClassMap.LookupClassMap(classType);
            }
        }

        public static async void CreateCollection(string name)
        {
            var filter = new BsonDocument("name", name);
            var options = new ListCollectionNamesOptions { Filter = filter };

            if ((await database.ListCollectionNamesAsync(options)).Any() == false)
            {
                await database.CreateCollectionAsync(name);

                var collection = database.GetCollection<Record>(name);

                var indexOptions = new CreateIndexOptions() { Unique = true };
                var collectionBuidler = Builders<Record>.IndexKeys;
                var indexModel = new CreateIndexModel<Record>(collectionBuidler.Ascending(x => x.key), indexOptions);

                await collection.Indexes.CreateOneAsync(indexModel);
            }
        }

        #endregion


        #region Unique Documents

        public static async void UpdateDocument(string collection, string key, object obj)
        {
            var coll = database.GetCollection<Record>(collection);
            var filter = Builders<Record>.Filter.Eq("key", key);
            var options = new UpdateOptions { IsUpsert = true };

            var json = JsonSerializer.Serialize(obj);
            var payload = BsonDocument.Parse(json);

            var update = Builders<Record>.Update.Set("payload", payload)
                                                      .Set("lastModificationDate", DateTime.Now);

            await coll.UpdateOneAsync(filter, update, options);
        }

        public static async void DeleteDocument(string collection, string key)
        {
            var coll = database.GetCollection<Record>(collection);

            var filter = Builders<Record>.Filter.Eq("key", key);

            await coll.DeleteManyAsync(filter);
        }

        public static T ReadDocument<T>(string collectionName, string key)
        {
            AddClassToMap(typeof(T));

            var collection = database.GetCollection<Record>(collectionName);

            var result = collection.Find(x => x.key == key).FirstOrDefault();

            if (result != null)
                return BsonSerializer.Deserialize<T>(result.payload);

            return default(T);
        }

        public static bool ReadDocument<T>(string collectionName, string key, out T document)
        {
            AddClassToMap(typeof(T));
            document = default(T);

            var collection = database.GetCollection<Record>(collectionName);
            var data = collection.Find(x => x.key == key).FirstOrDefault();

            if (data == null)
                return false;

            document = BsonSerializer.Deserialize<T>(data.payload);
            return true;
        }

        #endregion

    }

}
