<script setup lang="ts">
import { useStorage } from '@vueuse/core';
import { ItemType } from '@/models/item';
import { type AggregationConfig, AggregationView, type SortingConfig } from '@/models/item-search';
import { createItemIndex } from '@/services/item-search-service/indexator';
import { getSearchResult } from '@/services/item-search-service';
import {
  getClanArmoryItemLender,
  getClanArmoryItemBorrower,
  isOwnClanArmoryItem,
  isClanArmoryItemInInventory,
} from '@/services/clan-service';
import { notify } from '@/services/notification-service';
import { t } from '@/services/translate-service';
import { useClan } from '@/composables/clan/use-clan';
import { useClanArmory } from '@/composables/clan/use-clan-armory';
import { useItemDetail } from '@/composables/character/use-item-detail';
import { usePagination } from '@/composables/use-pagination';
import { useClanMembers } from '@/composables/clan/use-clan-members';
import { useUserStore } from '@/stores/user';
import { scrollToTop } from '@/utils/scroll';

definePage({
  props: true,
  meta: {
    layout: 'default',
    middleware: 'canUseClanArmory',
    roles: ['User', 'Moderator', 'Admin'],
  },
});

const props = defineProps<{
  id: string;
}>();

const userStore = useUserStore();

const { clanId, loadClan } = useClan(props.id);
const { clanArmory, loadClanArmory, isLoadingClanArmory, borrowItem, removeItem, returnItem } =
  useClanArmory(clanId.value);
const { clanMembers, loadClanMembers } = useClanMembers();

const onBorrowFromClanArmory = async (userItemId: number) => {
  await borrowItem(userItemId);
  await Promise.all([userStore.fetchUser(), userStore.fetchUserItems(), loadClanArmory()]);
  notify(t('clan.armory.item.borrow.notify.success'));
};

const onRemoveFromClanArmory = async (userItemId: number) => {
  await removeItem(userItemId);
  await Promise.all([userStore.fetchUser(), userStore.fetchUserItems(), loadClanArmory()]);
  notify(t('clan.armory.item.remove.notify.success'));
};

const onReturnFromClanArmory = async (userItemId: number) => {
  await returnItem(userItemId);
  await Promise.all([userStore.fetchUser(), userStore.fetchUserItems(), loadClanArmory()]);
  notify(t('clan.armory.item.return.notify.success'));
};

const sortingConfig: SortingConfig = {
  rank_desc: {
    field: 'rank',
    order: 'desc',
  },
  type_asc: {
    field: 'type',
    order: 'asc',
  },
};
const aggregationConfig = {
  type: {
    title: 'type',
    description: '',
    sort: 'term',
    size: 1000,
    conjunction: false,
    view: AggregationView.Radio,
    chosen_filters_on_top: false,
  },
} as AggregationConfig;

const sortingModel = ref<string>('rank_desc');
const filterByTypeModel = ref<ItemType[]>([]);
watch(filterByTypeModel, () => {
  pageModel.value = 1;
});
const filterByNameModel = ref<string>('');
const { pageModel } = usePagination();
const perPage = 24;
const hideOwnedItemsModel = useStorage<boolean>('clan-armory-hide-owned-items', true);
const showOnlyAvailableItems = useStorage<boolean>('clan-armory-show-only-available-items', true);

const flatItems = computed(() =>
  createItemIndex(
    clanArmory.value
      .filter(
        item =>
          (hideOwnedItemsModel.value ? !isOwnClanArmoryItem(item, userStore.user!.id) : true) &&
          (showOnlyAvailableItems.value
            ? item.borrowedItem === null &&
              !isOwnClanArmoryItem(item, userStore.user!.id) &&
              !isClanArmoryItemInInventory(item, userStore.userItems)
            : true)
      )
      .map(ca => ca.userItem.item)
  )
);

const searchResult = computed(() =>
  getSearchResult({
    items: flatItems.value,
    userItemsIds: [],
    aggregationConfig: aggregationConfig,
    sortingConfig: sortingConfig,
    sort: sortingModel.value,
    page: pageModel.value,
    perPage: perPage,
    query: filterByNameModel.value,
    filter: {
      type: filterByTypeModel.value,
    },
  })
);

const filteredClanArmoryItems = computed(() => {
  const foundedItemIds = searchResult.value.data.items.map(item => item.id);
  return clanArmory.value
    .filter(item => foundedItemIds.includes(item.userItem.item.id))
    .sort((a, b) => {
      if (sortingModel.value === 'type_asc') {
        const itemTypes = Object.values(ItemType);
        return itemTypes.indexOf(a.userItem.item.type) - itemTypes.indexOf(b.userItem.item.type);
      }
      return (
        foundedItemIds.indexOf(a.userItem.item.id) - foundedItemIds.indexOf(b.userItem.item.id)
      );
    });
});

const getClanArmoryItem = (userItemId: number) => {
  return clanArmory.value.find(ca => ca.userItem.id === userItemId);
};

const { toggleItemDetail, closeItemDetail } = useItemDetail();

await Promise.all([
  userStore.fetchUserItems(),
  loadClan(0, { id: clanId.value }),
  loadClanArmory(),
  loadClanMembers(0, { id: clanId.value }),
]);
</script>

<template>
  <div class="p-6">
    <OButton
      v-tooltip.bottom="$t('nav.back')"
      tag="router-link"
      :to="{ name: 'ClansId', params: { id: clanId } }"
      variant="secondary"
      size="xl"
      outlined
      rounded
      icon-left="arrow-left"
      data-aq-link="back-to-clan"
    />

    <div class="mx-auto max-w-2xl py-6">
      <Heading class="mb-14" :title="$t('clan.armory.title')" />

      <div v-if="clanArmory.length !== 0" class="itemGrid relative grid h-full gap-x-3 gap-y-4">
        <div style="grid-area: filter" class="space-y-2">
          <VDropdown :triggers="['click']" placement="bottom-end">
            <MoreOptionsDropdownButton :active="hideOwnedItemsModel || showOnlyAvailableItems" />

            <template #popper="{ hide }">
              <DropdownItem>
                <OCheckbox
                  v-model="hideOwnedItemsModel"
                  :nativeValue="true"
                  @change="
                    () => {
                      hide();
                      filterByTypeModel = [];
                    }
                  "
                >
                  {{ $t('clan.armory.filter.hideOwned') }}
                </OCheckbox>
              </DropdownItem>

              <DropdownItem>
                <OCheckbox
                  v-model="showOnlyAvailableItems"
                  :nativeValue="true"
                  @change="
                    () => {
                      hide();
                      filterByTypeModel = [];
                    }
                  "
                >
                  {{ $t('clan.armory.filter.showOnlyAvailable') }}
                </OCheckbox>
              </DropdownItem>
            </template>
          </VDropdown>

          <ItemGridFilter
            v-if="'type' in searchResult.data.aggregations"
            v-model="filterByTypeModel"
            :buckets="searchResult.data.aggregations.type.buckets"
            @click="scrollToTop"
          />
        </div>

        <div class="grid grid-cols-3 gap-4 2xl:grid-cols-4" style="grid-area: sort">
          <div class="col-span-2 2xl:col-span-3">
            <OInput
              v-model="filterByNameModel"
              type="text"
              expanded
              clearable
              :placeholder="$t('action.search')"
              icon="search"
              rounded
              size="sm"
            />
          </div>

          <ItemGridSearch class="col-span-1" v-model="sortingModel" :config="sortingConfig" />
        </div>

        <div style="grid-area: items">
          <OLoading v-if="isLoadingClanArmory" active iconSize="xl" />

          <div v-else class="relative grid grid-cols-3 gap-2 2xl:grid-cols-4">
            <ClanArmoryItemCard
              v-for="clanArmoryItem in filteredClanArmoryItems"
              :clanArmoryItem="clanArmoryItem"
              :lender="clanMembers.find(cm => cm.user.id === clanArmoryItem.userItem.userId)!.user"
              :borrower="getClanArmoryItemBorrower(clanArmoryItem, clanMembers)"
              @click="
                e =>
                  toggleItemDetail(e.target as HTMLElement, {
                    id: clanArmoryItem.userItem.item.id,
                    userItemId: clanArmoryItem.userItem.id,
                  })
              "
            />
          </div>
        </div>

        <div style="grid-area: footer" class="sticky bottom-4 z-10">
          <Pagination
            v-if="perPage < searchResult.pagination.total"
            class="justify-center"
            v-model="pageModel"
            :total="searchResult.pagination.total"
            :perPage="perPage"
          />
        </div>
      </div>

      <ResultNotFound
        v-else
        class="rounded-xl border border-dashed border-border-300"
        :message="$t('clan.armory.empty')"
      />
    </div>

    <ItemDetailGroup>
      <template #default="di">
        <ClanArmoryItemDetail
          :clanArmoryItem="getClanArmoryItem(di.userItemId)!"
          :lender="
            getClanArmoryItemLender(getClanArmoryItem(di.userItemId)!.userItem, clanMembers)!
          "
          :borrower="getClanArmoryItemBorrower(getClanArmoryItem(di.userItemId)!, clanMembers)"
          @borrow="
            id => {
              closeItemDetail(di);
              onBorrowFromClanArmory(id);
            }
          "
          @remove="
            id => {
              closeItemDetail(di);
              onRemoveFromClanArmory(id);
            }
          "
          @return="
            id => {
              closeItemDetail(di);
              onReturnFromClanArmory(id);
            }
          "
        />
      </template>
    </ItemDetailGroup>
  </div>
</template>

<style lang="css">
.itemGrid {
  grid-template-areas:
    '...... sort'
    'filter items'
    'filter footer';
  grid-template-columns: auto 1fr;
  grid-template-rows: auto 1fr auto;
}
</style>
