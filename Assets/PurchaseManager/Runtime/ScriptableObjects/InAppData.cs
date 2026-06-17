using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using IAPPurchasing.Utils;

namespace IAPPurchasing
{
    [CreateAssetMenu(fileName = "InAppData", menuName = "Create InAppData")]
    public class InAppData : ScriptableObject
    {
        public bool isTestMode;

        [Header("Products")]
        [SerializeField] private ProductData[] products;

        private string _testProductID = "android.test.purchased";

        private const string SettingsFileName = "InAppData";
        private const string SettingsFileExtension = ".asset";
        private const string ResDir = "Assets/Resources";

        public static InAppData GetInstance()
        {
            //Read from resources.
            var instance = Resources.Load<InAppData>(SettingsFileName);
            //Create instance if null.
            if (instance == null)
            {
                Directory.CreateDirectory(ResDir);
                instance = CreateInstance<InAppData>();
                string assetPath = Path.Combine(ResDir, SettingsFileName + SettingsFileExtension);
#if UNITY_EDITOR
                AssetDatabase.CreateAsset(instance, assetPath);
                AssetDatabase.SaveAssets();
#endif
            }
            return instance;
        }

        /// <summary>
        /// Get product list by product type
        /// </summary>
        public List<ProductData> GetProducts(bool IsConsumable)
        {
            var productList = new List<ProductData>();
            for (int i = 0; i < products.Length; i++)
            {
                if (IsConsumable && products[i].productType == ProductData.ProductType.Consumable)
                {
                    productList.Add(products[i]);
                }
                else if (!IsConsumable && products[i].productType != ProductData.ProductType.Consumable)
                {
                    productList.Add(products[i]);
                }
            }
            return productList;
        }

        public List<ProductData> GetAllProducts()
        {
            var productList = new List<ProductData>();
            for (int i = 0; i < products.Length; i++)
            {
                productList.Add(products[i]);

            }
            return productList;
        }

        public ProductData[] GetConsumableProducts()
        {
            return products;
        }

        public ProductData GetProductByID(string productId)
        {
            for (int i = 0; i < products.Length; i++)
            {
                if (productId == products[i].ProductId)
                {
                    return products[i];
                }
            }
            IAPDebug.Log("ProductData: GetProductByID: " + productId + " is not available");
            return null;
        }

        public void UpdateProductData(ProductData[] productData)
        {
            if (productData == null || productData.Length == 0)
            {
                IAPDebug.Log("UpdateProductData: Product data is null or empty");
                return;
            }
            products = new ProductData[productData.Length];
            for (int i = 0; i < productData.Length; i++)
            {
                products[i] = new ProductData(productData[i].name, productData[i].productType, productData[i].ProductId, productData[i].ProductId, productData[i].offer, productData[i].price); 
            }
        }
    }


    [Serializable]
    public class ProductData
    {
        public enum ProductType
        {
            Consumable = 0,
            NonConsumable = 1,
            Subscription = 2
        }
        public string name;
        public ProductType productType;
        [SerializeField] private string productIdAndroid;
        [SerializeField] private string productIdiOS;
        [Tooltip("Adding star on purchase this product")]
        public int offer;
        [Tooltip("Default pruduct price in dollor")]
        public float price;

        public string ProductId
        {
            get
            {
#if UNITY_ANDROID
                return productIdAndroid;
#elif UNITY_IOS
            return productIdiOS;
#endif
            }
        }

        public ProductData(string name, ProductType productType, string productIdAndroid, string productIdiOS, int offer, float price)
        {
            this.name = name;
            this.productType = productType;
            this.productIdAndroid = productIdAndroid;
            this.productIdiOS = productIdiOS;
            this.offer = offer;
            this.price = price;
        }
    }


}