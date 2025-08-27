using System;
using System.Collections.Generic;
using System.Linq; // Necessário para ToArray() e Where()
using System.Reflection;
using AmongUs.Data;
using HarmonyLib;
using UnityEngine;
using DateTime = Il2CppSystem.DateTime; // Para DateTime no IL2CPP

namespace UnlockAllCosmeticsMod
{
  

[HarmonyPatch(typeof(HatManager), nameof(HatManager.Initialize))]
    public static class CosmeticsUnlockPatch
    {
        // Cache simples de metadados para reduzir reflexão repetida
        private static readonly Dictionary<(Type,string), MemberInfo> memberCache = new Dictionary<(Type,string), MemberInfo>();
        private static readonly BindingFlags CaseInsensitiveInstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

        private static object ConvertIfNeeded(object value, Type targetType)
        {
            if (value == null) return null;
            var valueType = value.GetType();
            if (targetType.IsAssignableFrom(valueType)) return value;
            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return value;
            }
        }

        private static MemberInfo GetCachedMember(Type type, string name)
        {
            var key = (type, name.ToLowerInvariant());
            if (memberCache.TryGetValue(key, out var mi)) return mi;

            // tenta propriedade
            var prop = type.GetProperty(name, CaseInsensitiveInstanceFlags);
            if (prop != null)
            {
                memberCache[key] = prop;
                return prop;
            }
            // tenta campo
            var field = type.GetField(name, CaseInsensitiveInstanceFlags);
            if (field != null)
            {
                memberCache[key] = field;
                return field;
            }
            memberCache[key] = null;
            return null;
        }

        private static bool TrySetPropertyOrField(object target, object value, params string[] candidateNames)
        {
            if (target == null || candidateNames == null || candidateNames.Length == 0) return false;
            var type = target.GetType();
            foreach (var name in candidateNames)
            {
                var mi = GetCachedMember(type, name);
                if (mi is PropertyInfo prop && prop.CanWrite)
                {
                    try
                    {
                        var converted = ConvertIfNeeded(value, prop.PropertyType);
                        prop.SetValue(target, converted, null);
                        return true;
                    }
                    catch { /* ignora e tenta o próximo */ }
                }

                if (mi is FieldInfo field && !field.IsInitOnly)
                {
                    try
                    {
                        var converted = ConvertIfNeeded(value, field.FieldType);
                        field.SetValue(target, converted);
                        return true;
                    }
                    catch { /* ignora e tenta o próximo */ }
                }
            }
            return false;
        }

        private static string GetStringPropertyOrFieldOrDefault(object target, string defaultValue, params string[] candidateNames)
        {
            if (target == null || candidateNames == null || candidateNames.Length == 0) return defaultValue;
            var type = target.GetType();
            foreach (var name in candidateNames)
            {
                var mi = GetCachedMember(type, name);
                if (mi is PropertyInfo prop && prop.CanRead)
                {
                    try
                    {
                        var objVal = prop.GetValue(target, null);
                        if (objVal is string s && !string.IsNullOrEmpty(s)) return s;
                    }
                    catch { /* tenta próximo */ }
                }

                if (mi is FieldInfo field)
                {
                    try
                    {
                        var objVal = field.GetValue(target);
                        if (objVal is string s && !string.IsNullOrEmpty(s)) return s;
                    }
                    catch { /* tenta próximo */ }
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Patch aplicado ao método Initialize do HatManager para desbloquear todos os cosméticos.
        /// </summary>
        /// <param name="__instance">Instância do HatManager</param>
        public static void Postfix(HatManager __instance)
        {
            UnlockAllItems(__instance);
        }

        // Função utilitária para desbloquear e registrar como comprado
        private static void UnlockAndPurchaseAll<T>(T[] items, PlayerPurchasesData purchasesData) where T : class
        {
            if (items == null || items.Length == 0) return;
            foreach (var item in items)
            {
                if (item == null) continue;

                // Define o item como grátis, tentando diferentes nomes/casos
                TrySetPropertyOrField(item, true, "Free");

                // Marca como comprado quando houver ProductId/productId
                var productId = GetStringPropertyOrFieldOrDefault(item, null, "ProductId", "productId");
                if (!string.IsNullOrEmpty(productId))
                {
                    try { purchasesData?.SetPurchased(productId); }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[UnlockAllCosmeticsMod] Falha ao registrar compra de '{productId}': {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Desbloqueia todos os itens cosméticos.
        /// </summary>
        /// <param name="manager">Instância do HatManager contendo as listas de cosméticos</param>
        private static void UnlockAllItems(HatManager manager)
        {
            if (manager == null)
            {
                Debug.LogError("HatManager é nulo. Não foi possível desbloquear os itens.");
                return;
            }

            try
            {
                Debug.Log("[UnlockAllCosmeticsMod] Iniciando desbloqueio de todos os cosméticos...");
                var playerData = DataManager.Player;
                if (playerData == null)
                {
                    Debug.LogError("[UnlockAllCosmeticsMod] DataManager.Player é nulo. Abortando.");
                    return;
                }
                var purchasesData = playerData.Purchases;

                // 1. Filtrando pets válidos
                Debug.Log("[UnlockAllCosmeticsMod] Validando pets...");
                var validPets = manager.allPets != null ? manager.allPets.ToArray().Where(p => p != null).ToArray() : Array.Empty<PetData>();
                for (int i = 0; manager.allPets != null && i < manager.allPets.Count; i++)
                {
                    var pet = manager.allPets[i];
                    if (pet == null)
                    {
                        Debug.LogWarning($"[UnlockAllCosmeticsMod] Pet nulo encontrado no índice {i}");
                    }
                }

                // 2. Filtrando hats válidos
                var validHats = manager.allHats != null ? manager.allHats.ToArray().Where(h => h != null).ToArray() : Array.Empty<HatData>();

                // 3. Filtrando skins válidas
                var validSkins = manager.allSkins != null ? manager.allSkins.ToArray().Where(s => s != null).ToArray() : Array.Empty<SkinData>();

                // 4. Filtrando visors válidos
                var validVisors = manager.allVisors != null ? manager.allVisors.ToArray().Where(v => v != null).ToArray() : Array.Empty<VisorData>();

                // 5. Filtrando nameplates válidos
                var validNameplates = manager.allNamePlates != null ? manager.allNamePlates.ToArray().Where(n => n != null).ToArray() : Array.Empty<NamePlateData>();

                // 6. Validando bundles
                Debug.Log("[UnlockAllCosmeticsMod] Validando bundles...");
                var validBundles = new List<BundleData>();
                var bundlesSnapshot = manager.allBundles != null ? manager.allBundles.ToArray() : Array.Empty<BundleData>();
                foreach (var bundle in bundlesSnapshot)
                {
                    if (bundle == null) continue;

                    bool isValid = true;

                    // Verificando cosméticos no bundle
                    if (bundle.cosmetics != null)
                    {
                        foreach (var cosmetic in bundle.cosmetics)
                        {
                            if (cosmetic == null)
                            {
                                Debug.LogWarning($"[UnlockAllCosmeticsMod] Bundle '{bundle.productId}' contém um cosmético nulo. Pulando este bundle.");
                                isValid = false;
                                break;
                            }

                            // Verificando pets especificamente
                            if (cosmetic is PetData)
                            {
                                if (!validPets.Any(p => p.ProductId == cosmetic.ProductId))
                                {
                                    Debug.LogWarning($"[UnlockAllCosmeticsMod] Bundle '{bundle.productId}' referencia pet ausente '{cosmetic.ProductId}'. Pulando este bundle.");
                                    isValid = false;
                                    break;
                                }
                            }
                        }
                    }

                    if (isValid)
                    {
                        validBundles.Add(bundle);
                    }
                }

                Debug.Log($"[UnlockAllCosmeticsMod] {validBundles.Count} de {manager.allBundles.Count} bundles são válidos");

                // Desbloqueando apenas os itens válidos
                UnlockAndPurchaseAll<BundleData>(validBundles.ToArray(), purchasesData);
                UnlockAndPurchaseAll<HatData>(validHats, purchasesData);
                UnlockAndPurchaseAll<PetData>(validPets, purchasesData);
                UnlockAndPurchaseAll<SkinData>(validSkins, purchasesData);
                UnlockAndPurchaseAll<VisorData>(validVisors, purchasesData);
                UnlockAndPurchaseAll<NamePlateData>(validNameplates, purchasesData);

                try
                {
                    UnlockAndPurchaseAll<BundleData>(manager.allFeaturedBundles.ToArray().Where(b => b != null).ToArray(), purchasesData);
                    UnlockAndPurchaseAll<CosmicubeData>(manager.allFeaturedCubes.ToArray().Where(c => c != null).ToArray(), purchasesData);
                    UnlockAndPurchaseAll<CosmeticData>(manager.allFeaturedItems.ToArray().Where(i => i != null).ToArray(), purchasesData);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UnlockAllCosmeticsMod] Erro ao desbloquear itens em destaque: {ex.Message}");
                }

                // Definir o preço de todos os pacotes de estrelas como 0
                Debug.Log("[UnlockAllCosmeticsMod] Definindo preço de pacotes de estrelas como 0...");
                var starBundles = manager.allStarBundles != null ? manager.allStarBundles.ToArray() : Array.Empty<StarBundle>();
                foreach (var starBundle in starBundles)
                {
                    if (starBundle != null)
                    {
                        if (!TrySetPropertyOrField(starBundle, 0, "price", "Price"))
                        {
                            Debug.LogWarning("[UnlockAllCosmeticsMod] Não foi possível definir o preço do pacote de estrelas.");
                        }
                    }
                }

                // Atualizar as datas de visualização da loja
                var storeData = playerData.store;
                if (storeData != null)
                {
                    storeData.LastBundlesViewDate = DateTime.Now;
                    storeData.LastHatsViewDate = DateTime.Now;
                    storeData.LastOutfitsViewDate = DateTime.Now;
                    storeData.LastVisorsViewDate = DateTime.Now;
                    storeData.LastPetsViewDate = DateTime.Now;
                    storeData.LastNameplatesViewDate = DateTime.Now;
                    storeData.LastCosmicubeViewDate = DateTime.Now;
                }
                // Salvar as alterações
                playerData.Save();

                Debug.Log("[UnlockAllCosmeticsMod] Todos os cosméticos foram desbloqueados com sucesso!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnlockAllCosmeticsMod] Erro ao desbloquear cosméticos: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
    // Patch para ignorar a verificação de versão modificada
    [HarmonyPatch(typeof(Constants), "IsVersionModded")]
    public static class Constants_IsVersionModded_Patch
    {
        [HarmonyPrefix]
        public static bool ForceReturnFalse(ref bool __result)
        {
           
            __result = false;
            return false;
        }
    }

    // Patch para ignorar blacklist de cosméticos
    [HarmonyPatch(typeof(HatManager), nameof(HatManager.CheckValidCosmetic))]
    public static class Patch_IgnoreBlacklist
    {
        public static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }


        // Método auxiliar para validar um cosmético (verifica se é nulo ou possui propriedades inválidas)
        private static bool ValidateCosmetic(CosmeticData cosmetic, string itemType)
        {
            if (cosmetic == null)
            {
                Debug.LogWarning($"[UnlockAllCosmeticsMod] {itemType} nulo encontrado.");
                return false;
            }

            if (string.IsNullOrEmpty(cosmetic.ProductId))
            {
                Debug.LogWarning($"[UnlockAllCosmeticsMod] {itemType} '{cosmetic.name}' tem ProductId nulo ou vazio.");
                return false;
            }

            return true;

        }
    }

}
